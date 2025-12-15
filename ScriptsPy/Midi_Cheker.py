from pathlib import Path
import time
import tempfile
import sys
import json

from music21 import converter
import fluidsynth

def main():
    if len(sys.argv) < 2:
            raise RuntimeError("JSON string is required as argv[1]")
            
    #Json読み込み
    cmd = json.loads(sys.argv[1])
    xml_path = Path(cmd["MusicXML"])
    sf2_path = Path(cmd["Soundfont"])

    if not xml_path.exists():
        raise FileNotFoundError(f"MusicXML not found:{xml_path}")
    if not sf2_path.exists():
        raise FileNotFoundError(f"Soundfont(sf.2) not found:{sf2_path}")

    #MusicXML => MIDI
    score = converter.parse(str(xml_path))

    with tempfile.TemporaryDirectory() as td:
        midi_path = Path(td) / (xml_path.stem + ".mid")
        score.write("midi",fp=str(midi_path))

        fs = fluidsynth.Synth()
        fs.start(driver="dsound") #windows想定
        sfid = fs.sfload(str(sf2_path))
        fs.program_select(0,sfid,0,0)

        player = fluidsynth.Player(fs)
        player.add(str(midi_path))
        player.play

        while player.get_status() == fluidsynth.FLUID_PLAYER_PLAYING:
            time.sleep(0.05)

        player.stop()
        fs.delete()


if __name__ == "__main__":
    main()