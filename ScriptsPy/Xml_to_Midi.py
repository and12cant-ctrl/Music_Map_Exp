from pathlib import Path
import sys
import json
import subprocess
import warnings
from music21 import converter
from music21.musicxml.xmlToM21 import MusicXMLWarning

def main():
    try:
        raw = sys.stdin.read()
        if not raw.strip():
            raise ValueError("no JSON on stdin")
        cmd = json.loads(raw)
    except Exception:
        print(json.dumps({"ok": False, "error": "json parse error"}))
        return

    xml_path = Path(cmd.get("MusicXML", ""))
    sf2_path = Path(cmd.get("Soundfont", ""))
    fluid_path = Path(cmd.get("fluidsynth", ""))
    outputPath = Path(cmd.get("outputFolder", ""))

    if not xml_path.exists():
        print("ERROR: XML_pathがない", file=sys.stderr)
        raise FileNotFoundError(f"MusicXML not found:{xml_path}")
    if not sf2_path.exists():
        print("ERROR: Sf2がない", file=sys.stderr)
        raise FileNotFoundError(f"Soundfont(sf.2) not found:{sf2_path}")
    if not fluid_path.exists():
        print("ERROR: fluidsynthがない", file=sys.stderr)
        raise FileNotFoundError(f"fluidsynth not found:{fluid_path}")

    warnings.filterwarnings("ignore", category=MusicXMLWarning)

    score = converter.parse(str(xml_path))


    for el in score.recurse().notesAndRests:
        if not getattr(el, "voice", None):
            el.voice = "1"

    outputPath.mkdir(parents=True,exist_ok=True)
    midi_file = outputPath / f"{xml_path.stem}.mid"
    score.write("midi",fp=str(midi_file))
    # cmd = [
    #     str(fluid_path),
    #     "-i",            # interactive（鍵盤入力など）
    #     "-n",            # no shell（設定によっては不要だが残してOK）
    #     "-a", str(audio_driver),
    #     "-g", str(gain),
    #     "-o", f"synth.default-soundfont={str(sf2_path)}",
    #     str(midi_file),
    # ]

    cmd = [
    str(fluid_path),
    "-i", "-n",
    "-a", "dsound",
    "-o", f"synth.default-soundfont={str(sf2_path)}",
    str(midi_file),
]
    print("[INFO] 再生")
    subprocess.Popen(cmd)
    print("[INFO] 終了")

if __name__ == "__main__":
    main()