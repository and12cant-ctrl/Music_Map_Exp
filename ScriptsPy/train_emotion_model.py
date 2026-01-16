# train_emotion_lgbm.py
# Unity -> JSON受け取り -> 4CSV学習 -> モデル保存 -> 結果JSON(stdout)
#
# 使い方（例）
#   python -u train_emotion_lgbm.py "C:\Temp\unity_cmd_xxx.json"
# または
#   echo "{...json...}" | python -u train_emotion_lgbm.py --stdin

from __future__ import annotations

import json
import os
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Dict, Any, List, Optional

import pandas as pd
from sklearn.model_selection import train_test_split
from sklearn.metrics import classification_report, confusion_matrix
from lightgbm import LGBMClassifier


# ========== あなたの FINAL_COLUMNS（順番が命） ==========
FINAL_COLUMNS = [
    # basics
    "time_signature_num","time_signature_den","tempo_bpm_est","bars_len","total_duration_quarter","pickup_flag",

    # MAIN (melody)
    "pitch_std_midi","pitch_range_midi",
    *[f"pc_{i}" for i in range(12)],
    "interval_abs_mean_semitone","interval_abs_std_semitone",
    "step_ratio","leap_ratio","direction_up_ratio","direction_change_rate",
    "note_count_per_bar","onset_count_per_bar",
    "dur_mean_quarter","dur_std_quarter","short_note_ratio","rest_ratio_time","syncopation_score",
    "key_fit_ratio","chromatic_ratio","accidental_note_ratio","tonic_ending_flag",

    # ACC (accompaniment)
    "bass_mean_midi","bass_range_midi","bass_step_ratio","bass_leap_ratio",
    "chord_event_count_per_bar","mean_chord_size","chord_size_std",
    "polyphony_ratio_time","accomp_density_time",
    "dissonance_ratio","tritone_ratio","minor_third_ratio","major_third_ratio",
    "seventh_chord_ratio","sus_ratio",
    "key_fit_ratio_acc",
    *[f"pc_acc_{i}" for i in range(12)],
    "chromatic_ratio_acc","cadence_strength_score",
    "strongbeat_hit_ratio", "offbeat_ratio"
]


# ========== 入力JSONの想定 ==========
# {
#   "csvByLabel": {
#     "trust":   "C:/.../trust.csv",
#     "sad":     "C:/.../sad.csv",
#     "fear":    "C:/.../fear.csv",
#     "anxiety": "C:/.../anxiety.csv"
#   },
#   "outputDir": "C:/.../Models",          # ここに保存
#   "modelName": "emotion_lgbm",           # emotion_lgbm.txt / emotion_lgbm_meta.json
#   "testSize": 0.2,                       # 任意
#   "seed": 42,                            # 任意
#   "fillna": 0                            # 任意（既定0）
# }
#
# 互換： "csv_paths": [{"label":"trust","path":"..."} ...] でも可


@dataclass
class TrainConfig:
    csv_by_label: Dict[str, str]
    output_dir: Path
    model_name: str = "emotion_lgbm"
    test_size: float = 0.2
    seed: int = 42
    fillna_value: float = 0.0


def _read_json_from_file(path: Path) -> Dict[str, Any]:
    txt = path.read_text(encoding="utf-8")
    return json.loads(txt)


def _read_json_from_stdin() -> Dict[str, Any]:
    txt = sys.stdin.read()
    if not txt.strip():
        raise ValueError("stdinが空です。--stdin を使う場合はJSON文字列を標準入力に流してください。")
    return json.loads(txt)


def _normalize_config(obj: Dict[str, Any]) -> TrainConfig:
    # csvByLabel 方式
    csv_by_label = obj.get("csvByLabel")
    if isinstance(csv_by_label, dict) and csv_by_label:
        pass
    else:
        # csv_paths list 方式にも対応
        csv_paths = obj.get("csv_paths")
        if isinstance(csv_paths, list) and csv_paths:
            csv_by_label = {}
            for it in csv_paths:
                label = str(it.get("label", "")).strip()
                path = str(it.get("path", "")).strip()
                if label and path:
                    csv_by_label[label] = path
        else:
            csv_by_label = None

    if not csv_by_label or not isinstance(csv_by_label, dict):
        raise ValueError("入力JSONに csvByLabel も csv_paths も見つかりません。")

    out_dir = obj.get("outputDir") or obj.get("output_dir") or ""
    if not str(out_dir).strip():
        raise ValueError("入力JSONに outputDir が必要です。")
    output_dir = Path(out_dir)

    model_name = str(obj.get("modelName") or obj.get("model_name") or "emotion_lgbm")
    test_size = float(obj.get("testSize", 0.2))
    seed = int(obj.get("seed", 42))
    fillna_value = float(obj.get("fillna", 0.0))

    # 4クラスが揃っている前提（ただし将来拡張しやすいように厳密固定はしない）
    return TrainConfig(
        csv_by_label={str(k): str(v) for k, v in csv_by_label.items()},
        output_dir=output_dir,
        model_name=model_name,
        test_size=test_size,
        seed=seed,
        fillna_value=fillna_value,
    )


def _load_and_concat(csv_by_label: Dict[str, str]) -> pd.DataFrame:
    dfs = []
    for label, p in csv_by_label.items():
        path = Path(p)
        if not path.exists():
            raise FileNotFoundError(f"CSVが見つかりません: label={label} path={path}")

        df = pd.read_csv(path)

        # CSV内に label/emotion があるならそれを尊重しても良いが、
        # 今回は「ファイル単位でクラスが分かれている」前提なので確実に上書き
        df["label"] = label
        df["__source_csv"] = str(path)

        dfs.append(df)

    data = pd.concat(dfs, ignore_index=True)
    return data


def _train_lgbm(data: pd.DataFrame, cfg: TrainConfig):
    # 列存在チェック
    missing_cols = [c for c in FINAL_COLUMNS if c not in data.columns]
    if missing_cols:
        # 先頭だけ表示しつつ全数も返せるように
        raise ValueError(f"CSVに存在しない列があります: {missing_cols[:12]} ... (計{len(missing_cols)})")

    X = data[FINAL_COLUMNS].copy()
    y = data["label"].astype(str).copy()

    # 欠損埋め（treeはNaNでも可だが、運用のブレを減らす）
    X = X.fillna(cfg.fillna_value)

    # stratifyには「各クラス最低2件」が必要
    # （少ない場合は stratify なしに落とす）
    can_stratify = True
    vc = y.value_counts()
    if (vc < 2).any():
        can_stratify = False

    X_train, X_test, y_train, y_test = train_test_split(
        X, y,
        test_size=cfg.test_size,
        random_state=cfg.seed,
        stratify=y if can_stratify else None
    )

    clf = LGBMClassifier(
        n_estimators=900,
        learning_rate=0.03,
        num_leaves=63,
        subsample=0.9,
        colsample_bytree=0.9,
        random_state=cfg.seed,
        class_weight="balanced",
    )

    clf.fit(X_train, y_train)

    # 評価
    pred = clf.predict(X_test)
    cm = confusion_matrix(y_test, pred, labels=clf.classes_)
    report = classification_report(y_test, pred, digits=4, output_dict=True)

    # 重要度（gain）
    gain = clf.booster_.feature_importance(importance_type="gain")
    gain_top = (
        pd.Series(gain, index=FINAL_COLUMNS)
        .sort_values(ascending=False)
        .head(30)
        .to_dict()
    )

    return clf, {
        "classes": [str(c) for c in clf.classes_],
        "confusion_matrix": {
            "labels": [str(c) for c in clf.classes_],
            "matrix": cm.tolist(),
        },
        "report": report,              # precision/recall/f1/support など
        "gain_top30": gain_top,
        "n_rows_total": int(len(data)),
        "n_rows_train": int(len(X_train)),
        "n_rows_test": int(len(X_test)),
        "label_counts": {k: int(v) for k, v in data["label"].value_counts().to_dict().items()},
    }


def _save_outputs(clf: LGBMClassifier, cfg: TrainConfig, summary: Dict[str, Any]) -> Dict[str, str]:
    cfg.output_dir.mkdir(parents=True, exist_ok=True)

    model_path = cfg.output_dir / f"{cfg.model_name}.txt"
    meta_path = cfg.output_dir / f"{cfg.model_name}_meta.json"

    # LightGBMのモデル本体
    clf.booster_.save_model(str(model_path))

    # メタ情報（Unity推論で必須）
    meta = {
        "model_name": cfg.model_name,
        "model_path": str(model_path),
        "final_columns": FINAL_COLUMNS,   # 順番そのまま保存
        "classes": summary["classes"],    # class順番保存
        "preprocess": {
            "fillna": cfg.fillna_value,
        },
        "train_config": {
            "test_size": cfg.test_size,
            "seed": cfg.seed,
        },
        "metrics": {
            "confusion_matrix": summary["confusion_matrix"],
            "report": summary["report"],
        }
    }

    meta_path.write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")

    return {
        "model": str(model_path),
        "meta": str(meta_path),
    }


def main():
    try:
        # 入力：引数でJSONファイルパス or --stdin
        if len(sys.argv) >= 2 and sys.argv[1] == "--stdin":
            obj = _read_json_from_stdin()
        elif len(sys.argv) >= 2:
            json_path = Path(sys.argv[1])
            if not json_path.exists():
                raise FileNotFoundError(f"JSONファイルが見つかりません: {json_path}")
            obj = _read_json_from_file(json_path)
        else:
            raise ValueError("使い方: python -u train_emotion_lgbm.py <unity_cmd.json>  または  --stdin")

        cfg = _normalize_config(obj)

        data = _load_and_concat(cfg.csv_by_label)
        clf, summary = _train_lgbm(data, cfg)
        saved = _save_outputs(clf, cfg, summary)

        out = {
            "ok": True,
            "saved": saved,
            "summary": summary,
        }
        print(json.dumps(out, ensure_ascii=False))
        return 0

    except Exception as e:
        err = {
            "ok": False,
            "error": {
                "type": type(e).__name__,
                "message": str(e),
            }
        }
        # Unity側で拾いやすいように stdout にJSONで返す
        print(json.dumps(err, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
