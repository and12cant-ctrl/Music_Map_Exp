from pathlib import Path
import sys
import json
import os
import subprocess

from music21 import converter

def main():
    raw = sys.stdin.read()

    cmd = json.loads(raw)
    print("PYTHON ENTRY", flush=True)
    print("CMD =", cmd, flush=True)
    #MusicXML => MIDI
    xml_path    = Path(cmd["MusicXML"])
    sf2_path    = Path(cmd["Soundfont"])
    fluid_path  = Path(cmd["fluidsynth"])

    if not xml_path.exists():
        print("ERROR: XML_pathがない", file=sys.stderr)
        raise FileNotFoundError(f"MusicXML not found:{xml_path}")
    if not sf2_path.exists():
        print("ERROR: Sf2がない", file=sys.stderr)
        raise FileNotFoundError(f"Soundfont(sf.2) not found:{sf2_path}")
    if not fluid_path.exists():
        print("ERROR: fluidsynthがない", file=sys.stderr)
        raise FileNotFoundError(f"Soundfont(sf.2) not found:{sf2_path}")

    score = converter.parse(str(xml_path))


    midi_path = (xml_path.stem + ".mid")
    score.write("midi",fp=str(midi_path))


    print("[INFO] MIDI作成完了")
    print("[INFO] 再生開始")


    cmd = [fluid_path, "-i","-n","-a","dsound","-o", f"synth.default-soundfont={sf2_path}",  midi_path]
    subprocess.Popen(cmd)
    print("[INFO] 再生終了")



if __name__ == "__main__":
    main()