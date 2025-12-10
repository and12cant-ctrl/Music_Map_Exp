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
from pathlib import Path

from music21 import converter, meter, note, stream


# 許可する拍子
ALLOWED_TS = {"4/4", "3/4", "2/2"}

# 許可する音価（quarterLength）
ALLOWED_DURATIONS = {4.0, 2.0, 1.0, 0.5}


def pick_melody_part(score: stream.Score) -> stream.Part | stream.Stream:
    """
    一番ノート数が多いパートを「メロディパート」とみなして返す。
    パートが無い場合は score 自体を返す。
    """
    if getattr(score, "parts", None) and len(score.parts) > 0:
        return max(score.parts, key=lambda p: len(list(p.recurse().notes)))
    return score


def phrase_has_allowed_durations(phrase: stream.Stream) -> bool:
    """
    フレーズ内の音符・休符の duration が仕様に合っているかチェック。

    条件：
      - ノートが1つ以上存在すること
      - ノートの duration が ALLOWED_DURATIONS のみであること
      - duration が 0 以下のものが無いこと
    """
    elements = list(phrase.recurse().notesAndRests)
    if not any(isinstance(el, note.Note) for el in elements):
        return False

    for el in elements:
        ql = float(el.quarterLength)
        if ql <= 0:
            return False
        # 休符の duration は特に制限しないならここはスキップしてもよい
        if isinstance(el, note.Note) and ql not in ALLOWED_DURATIONS:
            return False

    return True


def extract_phrases_from_file(
    midi_path: Path,
    output_dir: Path,
    min_measures: int = 2,
    max_measures: int = 4,
    max_phrases_per_song: int = 50,
) -> int:
    """
    1つの MIDI ファイルから 2〜4小節フレーズを抽出し、MusicXML で出力する。

    戻り値：保存したフレーズ数
    """
    try:
        score = converter.parse(str(midi_path))
    except Exception as e:
        print(f"[ERROR] parse failed: {midi_path} ({e})")
        return 0

    part = pick_melody_part(score)

    # 拍子チェック
    ts = part.recurse().getElementsByClass(meter.TimeSignature).first()
    if ts is None:
        # 拍子が不明なものはスキップ
        print(f"[INFO] No TimeSignature, skip: {midi_path}")
        return 0

    ts_str = ts.ratioString  # "4/4" など
    if ts_str not in ALLOWED_TS:
        # 許可していない拍子はスキップ
        return 0

    # 小節一覧を取得
    measures = list(part.getElementsByClass("Measure"))
    if not measures:
        return 0

    # measure.number が None のものを除外して最大番号を求める
    numbers = [m.number for m in measures if m.number is not None]
    if not numbers:
        return 0
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
        return 0

    # ランダムシャッフルして上限まで出力
    random.shuffle(candidates)
    output_dir.mkdir(parents=True, exist_ok=True)

    saved = 0
    stem = midi_path.stem  # 元ファイル名

    for idx, (start_num, end_num, phrase) in enumerate(candidates):
        if saved >= max_phrases_per_song:
            break

        out_name = f"{stem}_m{start_num}-{end_num}_ts{ts_str}_{idx:03d}.musicxml"
        out_path = output_dir / out_name

        try:
            phrase.write("musicxml", fp=str(out_path))
            saved += 1
        except Exception as e:
            print(f"[ERROR] write failed: {out_path} ({e})")

    return saved


def main():
    parser = argparse.ArgumentParser(
        description="Lakh MIDI Dataset から 2〜4小節のフレーズを抽出して MusicXML として保存するスクリプト"
    )
    parser.add_argument(
        "input_root",
        help="LMD の MIDI が入っているルートディレクトリ (例: lmd_clean/)",
    )
    parser.add_argument(
        "output_root",
        help="抽出したフレーズを保存するディレクトリ",
    )
    parser.add_argument(
        "--min-measures",
        type=int,
        default=2,
        help="フレーズの最小小節数（デフォルト: 2）",
    )
    parser.add_argument(
        "--max-measures",
        type=int,
        default=4,
        help="フレーズの最大小節数（デフォルト: 4）",
    )
    parser.add_argument(
        "--max-phrases-per-song",
        type=int,
        default=50,
        help="1曲あたり保存するフレーズの上限（デフォルト: 50）",
    )
    parser.add_argument(
        "--max-songs",
        type=int,
        default=None,
        help="処理する曲数の上限（テスト用、デフォルト: 全曲）",
    )

    args = parser.parse_args()

    input_root = Path(args.input_root)
    output_root = Path(args.output_root)

    if not input_root.exists():
        print(f"[ERROR] input_root does not exist: {input_root}")
        return

    midi_files = [p for p in input_root.rglob("*") if p.suffix.lower() in (".mid", ".midi")]
    midi_files.sort()

    if args.max_songs is not None:
        midi_files = midi_files[: args.max_songs]

    print(f"[INFO] Found {len(midi_files)} MIDI files.")

    total_phrases = 0
    for i, midi_path in enumerate(midi_files, start=1):
        saved = extract_phrases_from_file(
            midi_path=midi_path,
            output_dir=output_root,
            min_measures=args.min_measures,
            max_measures=args.max_measures,
            max_phrases_per_song=args.max_phrases_per_song,
        )
        total_phrases += saved
        if saved > 0:
            print(f"[{i}/{len(midi_files)}] {midi_path.name}: {saved} phrases saved (total={total_phrases})")
        else:
            # 何も保存しなかった場合は静かめに
            pass

    print(f"[DONE] Total phrases saved: {total_phrases}")


if __name__ == "__main__":
    main()
