from __future__ import annotations
from pathlib import Path
from typing import Dict, Any, List
import pandas as pd

# ========= 1) 出力先 =========
BASE = Path(__file__).resolve().parent  # ScriptsPy
OUT_CSV = (BASE / "../MusicData/CSV/features.csv").resolve()
OUT_CSV.parent.mkdir(parents=True, exist_ok=True)

# ========= 2) CSVの「正」ヘッダ =========
CSV_COLUMNS: List[str] = [
    # meta
    "id", "src_file", "part_role", "start_bar", "end_bar", "duration_quarter",

    # melodic / pitch
    "mean_pitch", "pitch_std", "pitch_range",
    "interval_mean_abs", "interval_std",

    # rhythm / density
    "onset_density", "rest_ratio",

    # harmony / key
    "key_mode", "key_confidence",

    # label
    "emotion",
]

# ========= 3) extract_main_features の返り値キー → CSV列名 のマップ =========
# ここを「extract_main_features の実際のキー」に合わせて編集する
FEATURE_KEY_MAP: Dict[str, str] = {
    # CSV列名      : extract_main_features側のキー名
    "mean_pitch": "mean_pitch",
    "pitch_std": "pitch_std",
    "pitch_range": "pitch_range",
    "interval_mean_abs": "interval_mean_abs",
    "interval_std": "interval_std",
    "onset_density": "onset_count_per_bar",
    "rest_ratio": "rest_ratio",
    "key_mode": "key_mode",
    "key_confidence": "key_confidence",
}

def build_row(
    meta: Dict[str, Any],
    feats: Dict[str, Any],
    emotion: str,
) -> Dict[str, Any]:
    """
    meta: id/src_file/part_role/start_bar/end_bar/duration_quarter
    feats: extract_main_features の戻り値 dict
    emotion: trust/sad/fear/anxiety 等
    """
    row: Dict[str, Any] = {}

    # meta
    row["id"] = meta.get("id")
    row["src_file"] = meta.get("src_file")
    row["part_role"] = meta.get("part_role", "MAIN")
    row["start_bar"] = meta.get("start_bar")
    row["end_bar"] = meta.get("end_bar")
    row["duration_quarter"] = meta.get("duration_quarter")

    # features（マッピング）
    for csv_col, feat_key in FEATURE_KEY_MAP.items():
        row[csv_col] = feats.get(feat_key)

    # label
    row["emotion"] = emotion
    return row

def append_rows_to_csv(rows: List[Dict[str, Any]]):
    df_new = pd.DataFrame(rows)

    # 列順を固定（足りない列はNaN、余計な列は捨てる）
    df_new = df_new.reindex(columns=CSV_COLUMNS)

    if OUT_CSV.exists():
        df_new.to_csv(OUT_CSV, mode="a", header=False, index=False, encoding="utf-8-sig")
    else:
        df_new.to_csv(OUT_CSV, mode="w", header=True, index=False, encoding="utf-8-sig")

    print("Wrote rows:", len(df_new), "->", OUT_CSV)

# ========= 4) ここから下は既存処理に接続 =========
# 例：extract_main_features を import して、ループで rows を作る
#
# from your_module import extract_main_features, load_score_and_choose_main
#
# score, main_part, meta = load_score_and_choose_main(xml_path)
# feats = extract_main_features(main_part, score)

def main():
    # まず動作確認：ダミー feats（ここは後で消す）
    dummy_feats = {
        "mean_pitch": 64.3,
        "pitch_std": 2.1,
        "pitch_range": 7,
        "interval_mean_abs": 1.5,
        "interval_std": 0.8,
        "onset_count_per_bar": 1.75,  # ← FEATURE_KEY_MAPでonset_densityに流している例
        "rest_ratio": 0.12,
        "key_mode": "major",
        "key_confidence": 0.83,
    }
    meta = {
        "id": "dummy_001",
        "src_file": "SongA.musicxml",
        "part_role": "MAIN",
        "start_bar": 1,
        "end_bar": 2,
        "duration_quarter": 8.0,
    }
    row = build_row(meta, dummy_feats, emotion="trust")
    append_rows_to_csv([row])

if __name__ == "__main__":
    main()
