from pathlib import Path
import sys
import json
import warnings
from music21 import converter
from music21.musicxml.xmlToM21 import MusicXMLWarning

def main():
    # try:
    #     raw = sys.stdin.read()
    #     if not raw.strip():
    #         raise ValueError("no JSON on stdin")
    #     cmd = json.loads(raw)
    # except Exception:
    #     print(json.dumps({"ok": False, "error": "json parse error"}))
    #     return

    if len(sys.argv) < 2:
        print("Usage: python Xml_to_Midi.py <xml_path> [out_mid_path]", file=sys.stderr)
        sys.exit(2)

    xml_path = Path(sys.argv[1])
    if not xml_path.exists():
        print(f"[convert] MusicXML not found: {xml_path}", file=sys.stderr)
        sys.exit(2)

    if len(sys.argv) >= 3 and sys.argv[2].strip():
        out_mid = Path(sys.argv[2])
    else:
        out_mid = xml_path.with_suffix(".mid")

    # xml_path = Path(cmd.get("MusicXML", ""))
    # # sf2_path = Path(cmd.get("Soundfont", ""))
    # # fluid_path = Path(cmd.get("fluidsynth", ""))
    # outputPath = Path(cmd.get("outputFolder", ""))

    warnings.filterwarnings("ignore", category=MusicXMLWarning)

    score = converter.parse(str(xml_path))

    for el in score.recurse().notesAndRests:
        if not getattr(el, "voice", None):
            el.voice = "1"

    # outputPath.mkdir(parents=True,exist_ok=True)
    # midi_file = outputPath / f"{xml_path.stem}.mid"
    if out_mid.exists():
        out_mid.unlink()
    score.write("midi",fp=str(out_mid))
    # cmd = [
    #     str(fluid_path),
    #     "-i",            # interactive（鍵盤入力など）
    #     "-n",            # no shell（設定によっては不要だが残してOK）
    #     "-a", str(audio_driver),
    #     "-g", str(gain),
    #     "-o", f"synth.default-soundfont={str(sf2_path)}",
    #     str(midi_file),
    # ]
    print(json.dumps({"ok": True, "midi": str(out_mid)}))
    # cmd = [
    # str(fluid_path),
    # "-i", "-n",
    # "-a", "dsound",
    # "-o", f"synth.default-soundfont={str(sf2_path)}",
    # str(midi_file),
    # ]

if __name__ == "__main__":
    main()