#!/usr/bin/env python
"""
Lakh MIDI Dataset から 2〜4 小節の単一パートフレーズを大量抽出するスクリプト。

・入力：LMD の .mid ファイルが入ったディレクトリ（例: LMD-clean のルート）
・出力：2〜4小節のフレーズを MusicXML 形式で保存したディレクトリ

処理の流れ：
  1. 再帰的に .mid を探索
  2. 各ファイルを music21 で parse
  3. 「ノート数が最大のパート」をメロディパートとして選択
  4. 拍子が 4/4, 3/4, 2/2 以外ならスキップ
  5. 2〜4小節の連続フレーズをすべて候補とし、音価チェックを通ったものだけ採用
  6. 各MIDIファイルあたり最大 N フレーズまでを書き出し

"""

import argparse
import random
import sys
import json
import re
from pathlib import Path

from music21 import converter, meter, note, stream


# 許可する拍子
ALLOWED_TS = {"4/4", "3/4", "2/2"}

# 許可する音価（quarterLength）
ALLOWED_DURATIONS = {4.0, 2.0, 1.0, 0.5
                     ,0.25,1.5,3.0}


def pick_melody_part(score: stream.Score) -> stream.Part | stream.Stream:
    """
    一番ノート数が多いパートを「メロディパート」とみなして返す。
    パートが無い場合は score 自体を返す。
    """
    if getattr(score, "parts", None) and len(score.parts) > 0:
        return max(score.parts, key=lambda p: len(list(p.recurse().notes)))
    return score

def safeFolder(name:str) -> str:
     return re.sub(r'[<>:"/\\|?*\x00-\x1f]',"_",name).strip().strip(".")

def phrase_has_allowed_durations(phrase: stream.Stream,min_noteRatio:float = 0.8) -> bool:
    elements = list(phrase.recurse().notesAndRests)
    notes = [el for el in elements if isinstance(el,note.Note)]
    if not notes:
        return False    
    ok = 0

    for no in notes:
        ql = float(no.quarterLength)
        if ql <= 0:
            return False
        if ql in ALLOWED_DURATIONS:
            ok += 1

    ratio = ok /len(notes)
    return ratio >= min_noteRatio


def extract_phrases_from_file(
    midi_path: Path,
    output_dir: Path,
    min_measures: int = 2,
    max_measures: int = 4,
    max_phrases_per_song: int = 50,
) -> int:
    """
    1つの MIDI ファイルから 2〜4小節フレーズを抽出し、MusicXML で出力する。
    戻り値：保存したフレーズ数,保存したファイルパスのリスト
    """
    try:
        score = converter.parse(str(midi_path))
    except Exception as e:
        print(f"[ERROR] parse failed: {midi_path} ({e})", file=sys.stderr)
        return 0,[]

    part = pick_melody_part(score)

    # 拍子チェック
    ts = part.recurse().getElementsByClass(meter.TimeSignature).first()
    if ts is None:
        # 拍子が不明なものはスキップ
        print(f"[INFO] No TimeSignature, skip: {midi_path}", file=sys.stderr)
        return 0,[]

    ts_str = ts.ratioString  # "4/4" など
    if ts_str not in ALLOWED_TS:
        return 0,[]
    safe_ts =ts_str.replace("/","_")

    # 小節一覧を取得
    measures = list(part.getElementsByClass("Measure"))
    if not measures:
        return 0,[]

    # measure.number が None のものを除外して最大番号を求める
    numbers = [mea.number for mea in measures if mea.number is not None]
    if not numbers:
        return 0,[]
    last_number = max(numbers)

    # 2〜4小節のすべての連続フレーズを候補にする
    candidates: list[tuple[int, int, stream.Stream]] = []

    for length in range(min_measures, max_measures + 1):
        for start_num in range(1, last_number - length + 2):
            end_num = start_num + length - 1
            phrase = part.measures(start_num, end_num)
            if phrase is None:
                continue
            if phrase_has_allowed_durations(phrase):
                stats = calc_phrase_stats(phrase)
                e_score = score_energy(stats)
                s_score = score_stability(stats)
                candidates.append((start_num, end_num, phrase, e_score, s_score))

    if not candidates:
        return 0,[]

    best_energy = max(candidates, key=lambda x: x[3])   # energy score
    best_stable = max(candidates, key=lambda x: x[4])   # stability score

    selected = [best_energy]
    # 同じ区間が選ばれたら重複保存しない
    if (best_stable[0], best_stable[1]) != (best_energy[0], best_energy[1]):
        selected.append(best_stable)

    # 念のため：外部指定 max_phrases_per_song が 1 のときは 1つに抑える
    selected = selected[:max_phrases_per_song]

    output_dir.mkdir(parents=True, exist_ok=True)

    saved = 0
    outputs: list[str] = []
    stem = midi_path.stem
    song_dir = output_dir / safeFolder(stem)
    song_dir.mkdir(parents=True, exist_ok=True)

    for idx, (start_num, end_num, phrase, e_score, s_score) in enumerate(selected):
        out_name = f"{stem}_m{start_num}-{end_num}_ts{safe_ts}_E{e_score:.3f}_S{s_score:.3f}_{idx:03d}.musicxml"
        out_path = song_dir / out_name

        try:
            phrase.write("musicxml", fp=str(out_path))
            saved += 1
            outputs.append(str(out_path))
        except Exception as e:
            print(f"[ERROR] write failed: {out_path} ({e})", file=sys.stderr)

    return saved, outputs

def _clamp01(x: float) -> float:
    return 0.0 if x < 0.0 else 1.0 if x > 1.0 else x

def _norm(x: float, lo: float, hi: float) -> float:
    if hi <= lo:
        return 0.0
    return _clamp01((x - lo) / (hi - lo))

def calc_phrase_stats(phrase: stream.Stream) -> dict:
    """
    2〜4小節 phrase から簡易統計を取る（サビ候補スコア用）
    """
    elems = list(phrase.recurse().notesAndRests)
    notes = [e for e in elems if isinstance(e, note.Note)]
    rests = [e for e in elems if e.isRest]

    if not notes:
        return {
            "note_count": 0,
            "rest_ratio": 1.0,
            "pitch_range": 0.0,
            "avg_interval": 0.0,
            "avg_duration": 0.0,
            "density": 0.0,
        }

    # pitches
    mids = [n.pitch.midi for n in notes if getattr(n, "pitch", None) is not None]
    pitch_range = float(max(mids) - min(mids)) if mids else 0.0

    # durations (notes only)
    durs = [float(n.quarterLength) for n in notes]
    avg_duration = sum(durs) / len(durs) if durs else 0.0

    # avg interval (semitones) between consecutive notes
    # 順序は stream 順に依存（十分実用）
    intervals = []
    for i in range(1, len(mids)):
        intervals.append(abs(mids[i] - mids[i - 1]))
    avg_interval = (sum(intervals) / len(intervals)) if intervals else 0.0

    total = len(elems)
    rest_ratio = (len(rests) / total) if total > 0 else 0.0

    # note density: notes per quarter length
    total_quarters = float(getattr(phrase.duration, "quarterLength", 0.0) or 0.0)
    density = (len(notes) / total_quarters) if total_quarters > 0 else float(len(notes))

    return {
        "note_count": len(notes),
        "rest_ratio": float(rest_ratio),
        "pitch_range": float(pitch_range),
        "avg_interval": float(avg_interval),
        "avg_duration": float(avg_duration),
        "density": float(density),
    }

def score_energy(stats: dict) -> float:
    """
    fear/anxiety に強い「盛り上がり」スコア（大きいほどサビ候補）
    """
    # 正規化の目安（LMDでは幅があるのでゆるめ）
    n_pitch_range = _norm(stats["pitch_range"], 0.0, 24.0)   # 2オクターブで1.0
    n_avg_interval = _norm(stats["avg_interval"], 0.0, 12.0) # 1オクターブで1.0
    n_density = _norm(stats["density"], 0.0, 4.0)            # 1拍(quarter)あたり最大4音くらいで1.0

    return 0.45 * n_pitch_range + 0.35 * n_density + 0.20 * n_avg_interval

def score_stability(stats: dict) -> float:
    """
    trust/sad に強い「安定」スコア（大きいほどサビ候補）
    """
    n_avg_duration = _norm(stats["avg_duration"], 0.25, 4.0) # 16分〜全音符
    n_pitch_range = _norm(stats["pitch_range"], 0.0, 24.0)
    n_avg_interval = _norm(stats["avg_interval"], 0.0, 12.0)
    n_rest_ratio = _norm(stats["rest_ratio"], 0.0, 1.0)

    # 「安定」は、音価長め + 跳躍少なめ + 音域狭め + 休符少なめ(※多すぎは不安寄りになりがち) を好む
    return (
        0.40 * n_avg_duration +
        0.25 * (1.0 - n_avg_interval) +
        0.20 * (1.0 - n_pitch_range) +
        0.15 * (1.0 - n_rest_ratio)
    )



def main():

    try:
        raw = sys.stdin.read()
        if not raw.strip():
            raise ValueError("no JSON on stdin")
        cmd = json.loads(raw)
    except Exception as e :
        err = {"ok":False,"error":f"Json parse error: {e}"}
        print(json.dumps(err,ensure_ascii=False))
        return
    
    # 必須パラメータ
    midi_path = Path(cmd.get("midiPath", ""))

    output_dir = Path(cmd.get("outputDir", ""))
    if not output_dir:
        print(json.dumps({"ok":False,"error":"outputsDir is missing"},ensure_ascii=False))
        return

    if not midi_path.exists() or midi_path.is_dir():
        resp = {
            "ok": False,
            "error": f"Invalid MIDI path (not a file): {midi_path}",
        }
        print(json.dumps(resp, ensure_ascii=False))
        return
    # オプション（来てなければデフォルト）
    min_measures = int(cmd.get("minMeasures", 2))
    max_measures = int(cmd.get("maxMeasures", 4))
    max_phrases_per_song = int(cmd.get("maxPhrasesPerSong", 50))
    max_songs = cmd.get("maxSongs", None)
    if max_songs is not None:
        max_songs = int(max_songs)

    midi_path_str = cmd.get("midiPath", "")
    input_root_str = cmd.get("inputRoot", "")
    
    midi_files: list[Path] = []  

    if midi_path_str:
        p = Path(midi_path_str)
        if not p.exists() or p.is_dir():
            print(json.dumps({"ok": False, "error": f"Invalid MIDI path: {p}"}, ensure_ascii=False))
            return
        midi_files = [p]

    elif input_root_str:
        root = Path(input_root_str)
        if not root.exists() or not root.is_dir():
            print(json.dumps({"ok": False, "error": f"Invalid inputRoot: {root}"}, ensure_ascii=False))
            return
        midi_files = [p for p in root.rglob("*") if p.suffix.lower() in (".mid", ".midi")]
        midi_files.sort()
        if max_songs is not None:
            midi_files = midi_files[:max_songs]

    else:
        print(json.dumps({"ok": False, "error": "midiPath or inputRoot is required"}, ensure_ascii=False))
        return

    total_saved = 0
    all_outputs: list[str] = []
    per_song = []

    for mf in midi_files:
        saved, outputs = extract_phrases_from_file(
            midi_path=mf,
            output_dir=output_dir,
            min_measures=min_measures,
            max_measures=max_measures,
            max_phrases_per_song=max_phrases_per_song,
        )
        total_saved += saved
        all_outputs.extend(outputs)
        per_song.append({"midi": str(mf), "savedCount": saved})

    resp = {
        "ok": True,
        "totalSongs": len(midi_files),
        "savedCount": total_saved,
        "outputFiles": all_outputs,      # 既存互換
        "perSong": per_song,             # 追加情報（必要ならUnity側で受ける）
    }
    print(json.dumps(resp, ensure_ascii=False))

if __name__ == "__main__":
    main()
