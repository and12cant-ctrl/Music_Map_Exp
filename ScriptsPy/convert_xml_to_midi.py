#!/usr/bin/env python
# -*- coding: utf-8 -*-

import argparse
import os
import sys
from pathlib import Path
from typing import Optional

def eprint(*args, **kwargs):
    print(*args, file=sys.stderr, **kwargs)

def main():
    import argparse
    parser = argparse.ArgumentParser(
        description="Convert MusicXML (.xml/.musicxml) to MIDI (.mid)"
    )
    parser.add_argument("input", help="Path to MusicXML file (.xml / .musicxml)")
    parser.add_argument(
        "output",
        nargs="?",
        help="Output MIDI path (.mid). If omitted, uses <input>.mid next to input."
    )
    # 必要ならオプション例：--tempo 120 で強制テンポ
    parser.add_argument("--tempo", type=int, default=None,
                        help="Override tempo in BPM (optional)")

    args = parser.parse_args()

    in_path = Path(args.input).expanduser()
    if not in_path.is_absolute():
        in_path = (Path.cwd() / in_path)
    in_path = in_path.resolve()

    if not in_path.exists():
        eprint(f"[convert] Input not found: {in_path}")
        sys.exit(1)

    if in_path.suffix.lower() not in {".xml", ".musicxml"}:
        eprint(f"[convert] Unsupported input suffix: {in_path.suffix} "
               "(expected .xml / .musicxml)")
        sys.exit(1)

    if args.output:
        out_path = Path(args.output).expanduser()
        if not out_path.is_absolute():
            out_path = (Path.cwd() / out_path)
        out_path = out_path.resolve()
    else:
        out_path = in_path.with_suffix(".mid")

    # 出力先フォルダを作成（なければ）
    out_path.parent.mkdir(parents=True, exist_ok=True)

    # 変換本体
    try:
        from music21 import converter, tempo
        score = converter.parse(str(in_path))

        # 任意のテンポ上書き
        if args.tempo and args.tempo > 0:
            score.insert(0, tempo.MetronomeMark(number=args.tempo))

        score.write("midi", fp=str(out_path))
    except Exception as e:
        eprint(f"[convert] Failed to convert: {in_path.name}\n{e}")
        sys.exit(1)

    print(f"[convert] OK")
    print(f"[convert] input : {in_path}")
    print(f"[convert] output: {out_path}")
    sys.exit(0)

if __name__ == "__main__":
    main()

def eprint(*args,**kwargs):
    print(*args, file=sys.stderr, **kwargs)

def main():
    parser = argparse.ArgumentParser(
        description="Convert MusicXML (/.xml/.musicxml) to MIDI (.mid)"
        )       ##--help実行時に表示される説明文
    parser.add_argument("input",help="Path to MusicXML file (.xml / .musicxml)")
    parser.add_argument("output",nargs="?",
        help=
        "Output MIDI path (.mid). If omitted, uses <input>.mid next to input.")
    args = parser.parse_args()

    in_path = Path(args.input).expanduser().resolve()
    if not in_path.exists():
        print(f"[convert] Input not found: {in_path}", file=sys.stderr)
        sys.exit(1)

    if args.output:
        out_path = Path(args.output).expanduser().resolve()
    else:
        out_path = in_path.with_suffix(".mid")

    # ここから変換
    try:
        from music21 import converter
    except Exception as e:
        print("[convert] music21 is not installed. Run: pip install music21", file=sys.stderr)
        sys.exit(1)

    try:
        score = converter.parse(str(in_path))
        # 必要ならここで score.insert(0, tempo.MetronomeMark(number=120)) などを追加可能
        score.write("midi", fp=str(out_path))
    except Exception as e:
        # 例外は Unity 側で拾えるよう stderr に流す
        print(f"[convert] Failed to convert: {in_path.name}\n{e}", file=sys.stderr)
        sys.exit(1)

   # Unity から拾いやすい成功メッセージ（stdout, exit code 0）
    print(f"[convert] OK\nINPUT : {in_path}\nOUTPUT: {out_path}")
    sys.exit(0)

if __name__ == "__main__":
    main()