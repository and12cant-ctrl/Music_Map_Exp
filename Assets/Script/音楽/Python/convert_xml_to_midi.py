
import argparse
import os
import sys
from pathlib import Path

def main():
    parser = argparse.ArgumentParser(
        description="Convrt MusicXML (/.xml/.musicxml) to MIDI (.mid)"
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