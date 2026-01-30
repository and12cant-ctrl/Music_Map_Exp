# 感情ラベル付き特徴量CSVから、LightGBMの感情分類モデルを学習して保存する“学習専用スクリプト
# 
# {
#   "csvByLabel": {
#     "trust": "trust.csv",
#     "sad": "sad.csv",
#     "fear": "fear.csv",
#     "anxiety": "anxiety.csv"
#   },
#   "outputDir": "Models/emotion_lgbm",
#   "modelName": "emotion_model",
#   "testSize": 0.2,
#   "seed": 42,
#   "fillna": 0.0
# }

from __future__ import annotations

import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any, Dict, List, Optional

import numpy as np
import pandas as pd
from lightgbm import LGBMClassifier
from sklearn.metrics import classification_report, confusion_matrix
from sklearn.model_selection import train_test_split

from emotion_common import FINAL_COLUMNS


@dataclass
class TrainConfig:
    csv_by_label: Dict[str, str]
    output_dir: Path
    model_name: str = "emotion_model"
    test_size: float = 0.2
    seed: int = 42
    fillna_value: float = 0.0


def _read_json_from_file(path: Path) -> Dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _read_json_from_stdin() -> Dict[str, Any]:
    raw = sys.stdin.read()
    if not raw.strip():
        raise ValueError("stdin が空です。")
    return json.loads(raw)


def _read_json_from_path_or_stdin(args: List[str]) -> Dict[str, Any]:
    if len(args) >= 1 and args[0] == "--stdin":
        return _read_json_from_stdin()
    if len(args) >= 1:
        return _read_json_from_file(Path(args[0]))
    raise ValueError("JSONファイルパス or --stdin が必要です。")


def _normalize_config(obj: Dict[str, Any]) -> TrainConfig:
    csv_by_label = obj.get("csvByLabel")
    if not isinstance(csv_by_label, dict) or not csv_by_label:
        raise ValueError("csvByLabel が必要です（例: {trust: path, sad: path, ...}）。")

    out_dir = Path(obj.get("outputDir", "")).expanduser()
    if str(out_dir).strip() == "":
        raise ValueError("outputDir が必要です。")

    model_name = str(obj.get("modelName", "emotion_model"))
    test_size = float(obj.get("testSize", 0.2))
    seed = int(obj.get("seed", 42))
    fillna_value = float(obj.get("fillna", 0.0))

    return TrainConfig(
        csv_by_label={str(k): str(v) for k, v in csv_by_label.items()},
        output_dir=out_dir,
        model_name=model_name,
        test_size=test_size,
        seed=seed,
        fillna_value=fillna_value,
    )


def load_features_csv_with_order(csv_path: Path) -> pd.DataFrame:
    df = pd.read_csv(csv_path)
    # label列が無い場合はエラー（運用で揃える想定）
    if "label" not in df.columns:
        raise ValueError(f"CSVに label 列がありません: {csv_path}")

    # FINAL_COLUMNSが無い列は 0 で補完（安全）
    for c in FINAL_COLUMNS:
        if c not in df.columns:
            df[c] = 0.0

    # 列順固定
    cols = FINAL_COLUMNS + ["label"]
    return df[cols]


def _load_and_concat(cfg: TrainConfig) -> pd.DataFrame:
    frames: List[pd.DataFrame] = []
    for label, path_str in cfg.csv_by_label.items():
        p = Path(path_str)
        if not p.exists():
            raise FileNotFoundError(f"CSVが見つかりません: {p}")
        df = load_features_csv_with_order(p)
        # label上書き（CSV内labelを信用しない運用もOK）
        df["label"] = str(label)
        frames.append(df)
    return pd.concat(frames, ignore_index=True)


def _train_lgbm(data: pd.DataFrame, cfg: TrainConfig) -> Dict[str, Any]:
    X = data[FINAL_COLUMNS].copy().fillna(cfg.fillna_value)
    y = data["label"].astype(str).copy()

    # stratify最低条件（各クラス>=2件）
    vc = y.value_counts()
    can_stratify = bool((vc >= 2).all())
    stratify = y if can_stratify else None

    X_train, X_test, y_train, y_test = train_test_split(
        X, y,
        test_size=cfg.test_size,
        random_state=cfg.seed,
        stratify=stratify
    )

    clf = LGBMClassifier(
        objective="multiclass",
        random_state=cfg.seed,
        n_estimators=500,
        learning_rate=0.05,
        num_leaves=63,
    )
    clf.fit(X_train, y_train)

    y_pred = clf.predict(X_test)
    report = classification_report(y_test, y_pred, output_dict=True, zero_division=0)
    cm = confusion_matrix(y_test, y_pred, labels=sorted(y.unique()))

    summary = {
        "classes": list(clf.classes_),
        "confusion_matrix": cm.tolist(),
        "report": report,
        "counts": {k: int(v) for k, v in vc.to_dict().items()},
    }
    return {"clf": clf, "summary": summary}


def _save_outputs(clf: LGBMClassifier, cfg: TrainConfig, summary: Dict[str, Any]) -> Dict[str, str]:
    cfg.output_dir.mkdir(parents=True, exist_ok=True)
    model_path = cfg.output_dir / f"{cfg.model_name}.txt"
    meta_path = cfg.output_dir / f"{cfg.model_name}_meta.json"

    # Booster保存
    clf.booster_.save_model(str(model_path))

    meta = {
        "model_name": cfg.model_name,
        "model_path": str(model_path),
        "final_columns": FINAL_COLUMNS,
        "classes": summary["classes"],
        "preprocess": {"fillna": cfg.fillna_value},
        "train_config": {"test_size": cfg.test_size, "seed": cfg.seed},
        "metrics": {
            "confusion_matrix": summary["confusion_matrix"],
            "report": summary["report"],
        },
        "counts": summary.get("counts", {}),
    }
    meta_path.write_text(json.dumps(meta, ensure_ascii=False, indent=2), encoding="utf-8")

    return {"model": str(model_path), "meta": str(meta_path)}


def main() -> int:
    try:
        cmd = _read_json_from_path_or_stdin(sys.argv[1:])
        cfg = _normalize_config(cmd)

        data = _load_and_concat(cfg)
        trained = _train_lgbm(data, cfg)
        paths = _save_outputs(trained["clf"], cfg, trained["summary"])

        out = {"ok": True, "paths": paths, "summary": trained["summary"]}
        print(json.dumps(out, ensure_ascii=False))
        return 0

    except Exception as e:
        err = {"ok": False, "error": {"type": type(e).__name__, "message": str(e)}}
        print(json.dumps(err, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
