print("PYTHON STARTED", flush=True)
from pathlib import Path
import time
import tempfile
import sys
import json

from music21 import converter

def main():
    print("Test1")
    if len(sys.argv) < 2:
            print("ERROR: JSON string is required as argv[1]", file=sys.stderr)
            raise RuntimeError("JSON string is required as argv[1]")

    with open(args.root, "r", encoding="utf-8") as f:
        cmd = json.load(f)
    # Json読み込み
    json_path = Path(sys.argv[1])
    if not json_path.exists():
        print("ERROR: JSONファイルがない", file=sys.stderr)
        raise FileNotFoundError(f"JSON file not found: {json_path}")
    cmd = json.loads(json_path.read_text(encoding="utf-8"))

    #MusicXML => MIDI
    xml_path = Path(cmd["MusicXML"])
    sf2_path = Path(cmd["Soundfont"])
    if not xml_path.exists():
        print("ERROR: XML_pathがない", file=sys.stderr)
        raise FileNotFoundError(f"MusicXML not found:{xml_path}")
    if not sf2_path.exists():
        print("ERROR: Sf2がない", file=sys.stderr)
        raise FileNotFoundError(f"Soundfont(sf.2) not found:{sf2_path}")


    score = converter.parse(str(xml_path))

    with tempfile.TemporaryDirectory() as td:
        midi_path = Path(td) / (xml_path.stem + ".mid")
        score.write("midi",fp=str(midi_path))

        fs = fluidsynth.Synth()
        fs.start() #windows想定
        sfid = fs.sfload(str(sf2_path))
        fs.program_select(0,sfid,0,0)

        player = fluidsynth.Player(fs)
        player.add(str(midi_path))
        print("[INFO] MIDI作成完了")
        print("[INFO] 再生開始")
        player.play()

        while player.get_status() == fluidsynth.FLUID_PLAYER_PLAYING:
            time.sleep(0.05)
        print("[INFO] 再生終了")

        player.stop()
        fs.delete()


if __name__ == "__main__":
    main()