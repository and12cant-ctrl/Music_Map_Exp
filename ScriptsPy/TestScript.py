import pandas as pd
import numpy as np

CSV_PATH = r"G:\Unity\Product\Music_Map_Exp\MusicData\CSV\features.csv"  # ←ここだけ自分のCSVに変更

df = pd.read_csv(CSV_PATH)

print("Rows:", len(df), "Cols:", len(df.columns))

# 0) 列名の重複チェック（事故りやすい）
dups = pd.Index(df.columns)[pd.Index(df.columns).duplicated()].tolist()
if dups:
    print("❌ Duplicate columns found:", dups)
else:
    print("✅ No duplicate columns")

# 2)note_count_per_bar と onset_count_per_bar の同一率
#    完全一致が多すぎると「onsetが潰れてる」可能性
if "note_count_per_bar" in df.columns and "onset_count_per_bar" in df.columns:
    same = np.isclose(df["note_count_per_bar"], df["onset_count_per_bar"], atol=1e-9, rtol=0)
    same_ratio = same.mean() if len(df) > 0 else 0.0
    print(f"note_count_per_bar == onset_count_per_bar ratio: {same_ratio:.3f}")
    if same_ratio > 0.95:
        print("⚠️ ほとんど同じです。onset_count_per_bar が潰れている/計算が同じになっている可能性があります。")
    else:
        print("✅ onset_count_per_bar is not trivially identical to note_count_per_bar (good)")

# 3)strongbeat + offbeat ≈ 1.0
if "strongbeat_hit_ratio" in df.columns and "offbeat_ratio" in df.columns:
    s = df["strongbeat_hit_ratio"].fillna(0.0)
    o = df["offbeat_ratio"].fillna(0.0)
    sum_ok = np.isclose((s + o), 1.0, atol=1e-3, rtol=0)
    ok_ratio = sum_ok.mean() if len(df) > 0 else 0.0
    max_err = (s + o - 1.0).abs().max() if len(df) > 0 else 0.0
    print(f"(strongbeat + offbeat) ~= 1.0 ratio: {ok_ratio:.3f}, max_abs_error={max_err:.6f}")
    if ok_ratio < 0.95:
        print("⚠️ 合計が1になっていない行が多いです。unique offsetsの扱い・eps・bars計算を見直すと良いです。")
    else:
        print("✅ strongbeat/offbeat look consistent")

# 4) PC分布の合計が ≈ 1.0 になってるか（MAIN/ACC）
def check_pc_sum(prefix: str):
    cols = [f"{prefix}{i}" for i in range(12)]
    if any(c not in df.columns for c in cols):
        print(f"❌ Missing {prefix}* columns; skip sum check")
        return
    s = df[cols].fillna(0.0).sum(axis=1)
    ok = np.isclose(s, 1.0, atol=1e-3, rtol=0) | np.isclose(s, 0.0, atol=1e-12, rtol=0)  # 空なら0でもOK
    print(f"{prefix} sum ~= 1.0 (or 0.0 if empty) ratio: {ok.mean():.3f}, max={s.max():.6f}, min={s.min():.6f}")

check_pc_sum("pc_")
check_pc_sum("pc_acc_")

print("Done.")
