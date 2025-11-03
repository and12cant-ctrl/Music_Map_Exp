import os,sys
from midi2audio import FluidSynth

base_dir = os.path.dirname(os.path.abspath(__file__))

# SoundFont と FluidSynth の場所を絶対パスで指定
sf_path = os.path.abspath(os.path.join(base_dir, "..", "SoundFonts", "Arachno.sf2"))
fluidS_path = os.path.abspath(os.path.join(base_dir, "..",  "StreamingAssets", "scores","fluidsynth.exe"))

#存在チェック
print("Current Working Directory:", os.getcwd())
if len(sys.argv) < 2:
    print("Usage: python convert_midi_to_wav.py <midi_file>")
    sys.exit(1)

midi_path = sys.argv[1]
if not os.path.exists(midi_path):
    print(f"Error: MIDI file not found: {midi_path}")
    sys.exit(1)

wav_path = os.path.splitext(midi_path)[0]+".wav"

sf_path=os.path.join("SoundFonts","Arachno.sf2")
if not os.path.exists(sf_path):
    print(f"Warning: SoundFont not found: {sf_path}")
    sys.exit(1)

fs = FluidSynth(sound_font=sf_path,fluidsynth_path=fluidS_path)
fs.midi_to_audio(midi_path,wav_path)

print(f"[done] WAV created:{wav_path}")