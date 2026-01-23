# emotion_common.py
from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Dict, List, Tuple

import numpy as np
import lightgbm as lgb


# ======== FINAL_COLUMNS（順番が命）========
# ここはあなたの元ファイル(train_emotion_model.py)のFINAL_COLUMNSをそのままコピペしてください。
# 分割後は「ここが唯一の正」として運用します。
FINAL_COLUMNS: List[str] = [
    # TODO: ここに既存の FINAL_COLUMNS を丸ごと貼る
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
