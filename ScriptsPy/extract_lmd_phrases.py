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
                candidates.append((start_num, end_num, phrase))

    if not candidates:
        return 0,[]

    # ランダムシャッフルして上限まで出力
    random.shuffle(candidates)
    output_dir.mkdir(parents=True, exist_ok=True)

    saved = 0
    outputs: list[str] =[]
    stem = midi_path.stem  # 元ファイル名

    for idx, (start_num, end_num, phrase) in enumerate(candidates):
        if saved >= max_phrases_per_song:
            break

        out_name = f"{stem}_m{start_num}-{end_num}_ts{safe_ts}_{idx:03d}.musicxml"
        out_path = output_dir / out_name

        try:
            phrase.write("musicxml", fp=str(out_path))
            saved += 1
            outputs.append(str(out_path))
        except Exception as e:
            print(f"[ERROR] write failed: {out_path} ({e})")

    return saved,outputs


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

    if not midi_path.exists():
        resp = {
            "ok": False,
            "error": f"MIDI file not found: {midi_path}",
        }
        print(json.dumps(resp, ensure_ascii=False))
        return

    saved, outputs = extract_phrases_from_file(
        midi_path=midi_path,
        output_dir=output_dir,
        min_measures=min_measures,
        max_measures=max_measures,
        max_phrases_per_song=max_phrases_per_song,
    )

    resp = {
        "ok": True,
        "savedCount": saved,
        "outputFiles": outputs,
    }
    print(json.dumps(resp, ensure_ascii=False))


if __name__ == "__main__":
    main()
