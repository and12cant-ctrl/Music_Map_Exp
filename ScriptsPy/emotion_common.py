# emotion_common.py
from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, List, Tuple

import numpy as np
import lightgbm as lgb
import sys


# ======== FINAL_COLUMNS========
# 入力:特徴量
# 出力:感情スコア
FINAL_COLUMNS: List[str] = [
    # basics
    "time_signature_num","time_signature_den","tempo_bpm_est","bars_len","total_duration_quarter","pickup_flag",

    # MAIN (melody) - transposition-invariant + robust pitch stats
    "pitch_std_midi","pitch_range_midi",
    *[f"pc_{i}" for i in range(12)],
    "interval_abs_mean_semitone","interval_abs_std_semitone",
    "step_ratio","leap_ratio","direction_up_ratio","direction_change_rate",
    "note_count_per_bar","onset_count_per_bar",
    "dur_mean_quarter","dur_std_quarter","short_note_ratio","rest_ratio_time","syncopation_score",
    "key_fit_ratio","chromatic_ratio","accidental_note_ratio","tonic_ending_flag",

    # ACC (accompaniment) aggregated over matching ACC files
    "bass_mean_midi","bass_range_midi","bass_step_ratio","bass_leap_ratio",
    "chord_event_count_per_bar","mean_chord_size","chord_size_std",
    "polyphony_ratio_time","accomp_density_time",
    "dissonance_ratio","tritone_ratio","minor_third_ratio","major_third_ratio",
    "seventh_chord_ratio","sus_ratio",
    "key_fit_ratio_acc",
    *[f"pc_acc_{i}" for i in range(12)],
    "chromatic_ratio_acc","cadence_strength_score",
    "strongbeat_hit_ratio", "offbeat_ratio",
        # --- meta (non-numeric) ---
    "source_main_rel"
]


def load_meta(meta_path: Path) -> Dict[str, Any]:
    if not meta_path.exists():
        raise FileNotFoundError(f"meta が見つかりません: {meta_path}")
    return json.loads(meta_path.read_text(encoding="utf-8"))


def vectorize_features(
    feat_obj: Dict[str, Any],
    final_columns: List[str],
    fillna_value: float,
) -> np.ndarray:
    """
    入力：
      feat_obj:
        - {"featuresByName": {"col": 1.23, ...}}  または
        - {"featuresRow": [..final_columns順..]}
    出力：
      X: shape (1, len(final_columns))
    """
    if "featuresByName" in feat_obj:
        by_name = feat_obj["featuresByName"]
        if not isinstance(by_name, dict):
            raise ValueError("featuresByName は dict である必要があります。")
        row = []
        for c in final_columns:
            v = by_name.get(c, fillna_value)
            try:
                row.append(float(v))
            except Exception:
                row.append(float(fillna_value))
        return np.asarray([row], dtype=float)

    if "featuresRow" in feat_obj:
        row = feat_obj["featuresRow"]
        if not isinstance(row, list):
            raise ValueError("featuresRow は list である必要があります。")
        if len(row) != len(final_columns):
            raise ValueError(f"featuresRow の長さが不一致: got={len(row)} expected={len(final_columns)}")
        out = []
        for v in row:
            try:
                out.append(float(v))
            except Exception:
                out.append(float(fillna_value))
        return np.asarray([out], dtype=float)

    raise ValueError("featuresByName または featuresRow が必要です。")


def predict_with_saved_model(
    model_path: Path,
    meta: Dict[str, Any],
    feat_obj: Dict[str, Any],
) -> Dict[str, Any]:
    """
    LightGBM Booster(.txt) をロードして predict する。
    返り値は {label, proba_by_class, classes}
    """
    final_columns = meta.get("final_columns", FINAL_COLUMNS)
    classes = meta.get("classes")
    fillna = float(meta.get("preprocess", {}).get("fillna", 0.0))

    if not isinstance(final_columns, list) or not final_columns:
        raise ValueError("meta に final_columns がありません。")
    if not isinstance(classes, list) or not classes:
        raise ValueError("meta に classes がありません。")
    if not model_path.exists():
        raise FileNotFoundError(f"model が見つかりません: {model_path}")

    X = vectorize_features(feat_obj, final_columns, fillna)

    booster = lgb.Booster(model_file=str(model_path))
    proba = booster.predict(X)  # multiclass => (1, num_class) or (num_class,)

    proba = np.asarray(proba)
    if proba.ndim == 1:
        proba = proba.reshape(1, -1)

    if proba.shape[1] != len(classes):
        raise ValueError(f"proba次元とclasses数が不一致: proba={proba.shape} classes={len(classes)}")

    probs = proba[0].tolist()
    best_idx = int(np.argmax(proba[0]))
    label = str(classes[best_idx])

    return {
        "label": label,
        "classes": classes,
        "proba_by_class": {str(classes[i]): float(probs[i]) for i in range(len(classes))},
        "proba_vec": [float(x) for x in probs],
    }


def read_json_from_file(path: Path) -> Dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def read_json_from_stdin() -> Dict[str, Any]:
    raw = sys.stdin.read()
    if not raw.strip():
        raise ValueError("stdin が空です。")
    return json.loads(raw)

# ======== CLI: CSV + row -> emotion JSON ========
import argparse
import csv as _csv

def _read_csv_row_as_features_by_name(csv_path: Path, row_index: int) -> Dict[str, Any]:
    """
    CSV(ヘッダあり)から指定行を取り出して featuresByName を作る。
    row_index:
      - 0..N-1: その行
      - -1: 最終行
    """
    # Excel互換でutf-8-sigの可能性が高いのでまずこれで読む
    # （utf-8でも普通に読める）
    with csv_path.open("r", encoding="utf-8-sig", newline="") as f:
        reader = _csv.DictReader(f)
        rows = list(reader)

    if not rows:
        raise ValueError("CSVが空です。")

    idx = row_index if row_index >= 0 else (len(rows) - 1)
    if idx < 0 or idx >= len(rows):
        raise ValueError(f"rowが範囲外です: row={row_index} (len={len(rows)})")

    r = rows[idx]

    # featuresByName：数値列だけ詰める（source_main_rel は非数値なので除外）
    by_name: Dict[str, Any] = {}
    for c in FINAL_COLUMNS:
        if c == "source_main_rel":
            continue
        by_name[c] = r.get(c, 0.0)

    return {"featuresByName": by_name}


def cli_main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--csv", required=True, help="feature CSV path (header required)")
    ap.add_argument("--row", type=int, default=-1, help="row index (0-based). -1 means last row.")
    ap.add_argument("--model", required=True, help="emotion_lgbm.txt path")
    ap.add_argument("--meta", required=True, help="emotion_lgbm_meta.json path")
    args = ap.parse_args()

    csv_path = Path(args.csv)
    model_path = Path(args.model)
    meta_path = Path(args.meta)

    meta = load_meta(meta_path)
    feat_obj = _read_csv_row_as_features_by_name(csv_path, args.row)

    out = predict_with_saved_model(
        model_path=model_path,
        meta=meta,
        feat_obj=feat_obj,
    )

    print(json.dumps(out, ensure_ascii=False))


if __name__ == "__main__":
    # 既存の import 用モジュールとしても使えるように、
    # 直接実行された時だけ CLI を動かす
    cli_main()
