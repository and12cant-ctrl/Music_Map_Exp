# tag_templates_4axis.py
# 既存の特徴量CSV（FINAL_COLUMNSを含む）から、
# 4軸（motion/density/order/rhythm）のスコアを作り、分位(q33/q66)で Low/Mid/High を付与して出力する。
#
# 入力：
#   1) 単一CSV もしくは 2) フォルダ内のCSV全て もしくは 3) 複数CSV指定
# 出力：
#   - tagged_templates.csv（元列 + axis_score + axis_band）
#   - thresholds_4axis.json（各軸のq33/q66と正規化レンジ等）

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Dict, List, Tuple

import numpy as np
import pandas as pd

import lightgbm as lgb
import emotion_common

# ====== 4軸で使う列（あなたのFINAL_COLUMNSから流用）======
MOTION_COLS = [
    "leap_ratio",
    "direction_change_rate",
    "pitch_range_midi",
]

DENSITY_COLS = [
    "note_count_per_bar",
    "rest_ratio_time",     # 逆向き
    "short_note_ratio",
]

ORDER_COLS = [
    "key_fit_ratio",
    "chromatic_ratio",     # 逆向き
    # pc集中度は pc_0..11 から派生で作る
]

RHYTHM_COLS = [
    "strongbeat_hit_ratio",
    "syncopation_score",   # 逆向き
    "dur_std_quarter",     # 逆向き
]


# ====== 重み（必要なら後で調整）======
WEIGHTS = {
    "motion": {"leap_ratio": 0.40, "direction_change_rate": 0.30, "pitch_range_midi": 0.30},
    "density": {"note_count_per_bar": 0.45, "rest_ratio_time": 0.35, "short_note_ratio": 0.20},
    "order": {"key_fit_ratio": 0.50, "chromatic_ratio": 0.25, "pc_concentration": 0.25},
    "rhythm": {"strongbeat_hit_ratio": 0.45, "syncopation_score": 0.30, "dur_std_quarter": 0.25},
}

# 逆向き（値が大きいほど「低」側になる）を正方向に揃えるため、1 - norm を使う列
INVERT_COLS = {
    "rest_ratio_time",
    "chromatic_ratio",
    "syncopation_score",
    "dur_std_quarter",
}


def pc_concentration_from_row(row: pd.Series) -> float:
    pcs = []
    for i in range(12):
        v = row.get(f"pc_{i}", 0.0)
        try:
            pcs.append(float(v))
        except Exception:
            pcs.append(0.0)
    arr = np.asarray(pcs, dtype=float)
    # 集中度：max - mean（簡単で効く）
    return float(np.max(arr) - np.mean(arr))


def ensure_columns(df: pd.DataFrame, cols: List[str], fillna: float = 0.0) -> pd.DataFrame:
    for c in cols:
        if c not in df.columns:
            df[c] = fillna
    return df


def robust_minmax_params(series: pd.Series, low_q: float = 0.02, high_q: float = 0.98) -> Tuple[float, float]:
    """外れ値に強いmin/maxを分位点で作る"""
    s = pd.to_numeric(series, errors="coerce").fillna(0.0)
    lo = float(s.quantile(low_q))
    hi = float(s.quantile(high_q))
    if hi - lo < 1e-12:
        # ほぼ定数の場合
        lo = float(s.min())
        hi = float(s.max())
        if hi - lo < 1e-12:
            hi = lo + 1.0
    return lo, hi


def norm01(x: pd.Series, lo: float, hi: float) -> pd.Series:
    s = pd.to_numeric(x, errors="coerce").fillna(0.0)
    v = (s - lo) / (hi - lo)
    return v.clip(0.0, 1.0)


def compute_axis_scores(df: pd.DataFrame) -> Tuple[pd.DataFrame, Dict]:
    """
    df に以下を追加：
      motion_score, density_score, order_score, rhythm_score （0..1）
    返り値の params には正規化レンジ等を入れる
    """
    df = df.copy()

    # 必要列を保証
    df = ensure_columns(df, MOTION_COLS + DENSITY_COLS + ORDER_COLS + RHYTHM_COLS)
    for i in range(12):
        if f"pc_{i}" not in df.columns:
            df[f"pc_{i}"] = 0.0

    # 派生：pc集中度
    df["pc_concentration"] = df.apply(pc_concentration_from_row, axis=1)

    # 正規化レンジ（軸ごとではなく列ごとに持つ）
    all_cols = list(set(MOTION_COLS + DENSITY_COLS + ORDER_COLS + RHYTHM_COLS + ["pc_concentration"]))
    norm_params: Dict[str, Dict[str, float]] = {}

    for c in all_cols:
        lo, hi = robust_minmax_params(df[c])
        norm_params[c] = {"lo": lo, "hi": hi}

    # 正規化の実体
    normed: Dict[str, pd.Series] = {}
    for c in all_cols:
        lo = norm_params[c]["lo"]
        hi = norm_params[c]["hi"]
        v = norm01(df[c], lo, hi)
        if c in INVERT_COLS:
            v = 1.0 - v
        normed[c] = v

    # 軸スコア（0..1）
    df["motion_score"] = (
        WEIGHTS["motion"]["leap_ratio"] * normed["leap_ratio"]
        + WEIGHTS["motion"]["direction_change_rate"] * normed["direction_change_rate"]
        + WEIGHTS["motion"]["pitch_range_midi"] * normed["pitch_range_midi"]
    )

    df["density_score"] = (
        WEIGHTS["density"]["note_count_per_bar"] * normed["note_count_per_bar"]
        + WEIGHTS["density"]["rest_ratio_time"] * normed["rest_ratio_time"]
        + WEIGHTS["density"]["short_note_ratio"] * normed["short_note_ratio"]
    )

    df["order_score"] = (
        WEIGHTS["order"]["key_fit_ratio"] * normed["key_fit_ratio"]
        + WEIGHTS["order"]["chromatic_ratio"] * normed["chromatic_ratio"]
        + WEIGHTS["order"]["pc_concentration"] * normed["pc_concentration"]
    )

    df["rhythm_score"] = (
        WEIGHTS["rhythm"]["strongbeat_hit_ratio"] * normed["strongbeat_hit_ratio"]
        + WEIGHTS["rhythm"]["syncopation_score"] * normed["syncopation_score"]
        + WEIGHTS["rhythm"]["dur_std_quarter"] * normed["dur_std_quarter"]
    )

    params = {
        "norm_params": norm_params,
        "weights": WEIGHTS,
        "invert_cols": sorted(list(INVERT_COLS)),
    }
    return df, params


def band_by_quantiles(scores: pd.Series, q1: float = 0.33, q2: float = 0.66) -> Tuple[pd.Series, float, float]:
    s = pd.to_numeric(scores, errors="coerce").fillna(0.0)
    t1 = float(s.quantile(q1))
    t2 = float(s.quantile(q2))
    # 同値が多いとt1==t2になるので、軽く逃がす
    if abs(t2 - t1) < 1e-12:
        t2 = t1 + 1e-6

    def _band(v: float) -> str:
        if v < t1:
            return "low"
        elif v < t2:
            return "mid"
        else:
            return "high"

    bands = s.apply(_band)
    return bands, t1, t2


def load_inputs(inputs: List[str], from_dir: bool = False) -> pd.DataFrame:
    paths: List[Path] = []
    for inp in inputs:
        p = Path(inp)
        if p.is_dir():
            paths.extend(sorted(p.glob("*.csv")))
        else:
            paths.append(p)

    if not paths:
        raise FileNotFoundError("入力CSVが見つかりません。パス/フォルダを確認してください。")

    frames = []
    for p in paths:
        df = pd.read_csv(p)
        df["__src_csv"] = str(p)
        frames.append(df)
    return pd.concat(frames, ignore_index=True)

def add_emotion_columns(
    df: pd.DataFrame,
    model_path: Path,
    meta_path: Path,
) -> pd.DataFrame:
    """
    df の各行について LightGBM 推論を行い、以下の列を追加して返す：
      emo_<class>（各クラス確率）
      emo_max_label, emo_max_score
      emo_min_label, emo_min_score
    """
    df = df.copy()

    meta = emotion_common.load_meta(meta_path)  # :contentReference[oaicite:3]{index=3}
    final_columns = meta.get("final_columns", emotion_common.FINAL_COLUMNS)
    classes = meta.get("classes")
    fillna = float(meta.get("preprocess", {}).get("fillna", 0.0))

    if not isinstance(classes, list) or not classes:
        raise ValueError("meta に classes がありません。")  # :contentReference[oaicite:4]{index=4}

    booster = lgb.Booster(model_file=str(model_path))

    # 出力列を先に作る（順序安定）
    for c in classes:
        col = f"emo_{c}"
        if col not in df.columns:
            df[col] = 0.0

    df["emo_max_label"] = ""
    df["emo_max_score"] = 0.0
    df["emo_min_label"] = ""
    df["emo_min_score"] = 0.0

    # 行ごと推論
    for idx, row in df.iterrows():  # ← indexラベルをそのまま使う
        by_name = {}
        for col in final_columns:
            if col == "source_main_rel":
                continue
            v = row.get(col, fillna)
            by_name[col] = v

        X = emotion_common.vectorize_features(
            {"featuresByName": by_name},
            final_columns=[c for c in final_columns if c != "source_main_rel"],
            fillna_value=fillna,
        )

        proba = booster.predict(X)
        import numpy as _np
        proba = _np.asarray(proba)
        if proba.ndim == 1:
            proba = proba.reshape(1, -1)
        probs = proba[0].tolist()

        # 各感情列へ（idxで書く）
        for k, cls in enumerate(classes):
            df.at[idx, f"emo_{cls}"] = float(probs[k])

        max_i = int(_np.argmax(proba[0]))
        min_i = int(_np.argmin(proba[0]))

        df.at[idx, "emo_max_label"] = str(classes[max_i])
        df.at[idx, "emo_max_score"] = float(probs[max_i])
        df.at[idx, "emo_min_label"] = str(classes[min_i])
        df.at[idx, "emo_min_score"] = float(probs[min_i])


    return df

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("inputs", nargs="+", help="CSVファイル or CSVフォルダ を指定（複数OK）")
    ap.add_argument("--out_csv", type=str, default="tagged_templates.csv", help="出力CSV")
    ap.add_argument("--out_json", type=str, default="thresholds_4axis.json", help="出力JSON（閾値など）")
    ap.add_argument("--q1", type=float, default=0.33, help="low/midの境界分位（デフォ0.33）")
    ap.add_argument("--q2", type=float, default=0.66, help="mid/highの境界分位（デフォ0.66）")
    ap.add_argument("--emo_model", type=str, default="", help="emotion_lgbm.txt（指定すると感情列も付与）")
    ap.add_argument("--emo_meta", type=str, default="", help="emotion_lgbm_meta.json（指定すると感情列も付与）")

    args = ap.parse_args()

    df = load_inputs(args.inputs)
    df2, params = compute_axis_scores(df)

    # Low/Mid/High の付与（軸ごと）
    thresholds = {}
    for axis in ["motion", "density", "order", "rhythm"]:
        col = f"{axis}_score"
        bands, t1, t2 = band_by_quantiles(df2[col], q1=args.q1, q2=args.q2)
        df2[f"{axis}_band"] = bands
        thresholds[axis] = {"q1": args.q1, "q2": args.q2, "t1": t1, "t2": t2}
            # 感情列の付与（オプション）

    print("DEBUG emo cols after add:", [c for c in df2.columns if c.startswith("emo_")])
    if args.emo_model and args.emo_meta:
        df2 = add_emotion_columns(df2, Path(args.emo_model), Path(args.emo_meta))
    # 出力
    print(df2[[c for c in df2.columns if c.startswith("emo_")]].head(3))
    out_csv = Path(args.out_csv)
    out_csv.parent.mkdir(parents=True, exist_ok=True)

    # KEEP_COLS = [

    #     "motion_score", "density_score", "order_score", "rhythm_score",
    #     "motion_band", "density_band", "order_band", "rhythm_band",
    #     "emo_trust", "emo_fear", "emo_sad", "emo_anxiety",
    #     "emo_max_label", "emo_max_score",
    #     "emo_min_label", "emo_min_score",
    #     "source_main_rel",
    # ]
    KEEP_COLS = [
    "motion_score", "density_score", "order_score", "rhythm_score",
    "motion_band", "density_band", "order_band", "rhythm_band",
    ]

    # 感情列（動的）
    if args.emo_model and args.emo_meta:
        import emotion_common
        meta = emotion_common.load_meta(Path(args.emo_meta))
        classes = meta.get("classes", [])

        KEEP_COLS += [f"emo_{c}" for c in classes]
        KEEP_COLS += [
            "emo_max_label", "emo_max_score",
            "emo_min_label", "emo_min_score",
        ]

    KEEP_COLS += ["source_main_rel"]

    # 入力側に source_main_rel が無いケースに備えて空列を作る
    for c in KEEP_COLS:
        if c not in df2.columns:
            df2[c] = ""

    df_out = df2[KEEP_COLS]
    df_out.to_csv(out_csv, index=False, encoding="utf-8-sig")
    print("DEBUG emo cols before output:", [c for c in df2.columns if c.startswith("emo_")])
    print("DEBUG KEEP_COLS has emo?:", [c for c in KEEP_COLS if str(c).startswith("emo_")]) 

    out_json = Path(args.out_json)
    out_json.parent.mkdir(parents=True, exist_ok=True)
    payload = {
        "thresholds": thresholds,
        **params,
        "notes": {
            "bands": {"low": f"< q{args.q1}", "mid": f"q{args.q1}..q{args.q2}", "high": f">= q{args.q2}"},
            "axis_scores_range": "0..1",
            "pc_concentration": "max(pc_0..11)-mean(pc_0..11)",
            "normalization": "robust minmax by quantiles (2%..98%), then clip 0..1",
        },
    }
    out_json.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    print(json.dumps({"ok": True, "out_csv": str(out_csv), "out_json": str(out_json)}, ensure_ascii=False))

if __name__ == "__main__":
    main()
