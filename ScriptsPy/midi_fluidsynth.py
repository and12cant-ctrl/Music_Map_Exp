import argparse
import os
import sys
import subprocess

def main():

    ap = argparse.ArgumentParser()
    ap.add_argument("--root", help="Unity project root path")
    ap.add_argument("--sf2",  help="Custom SoundFont path")
    ap.add_argument("--mid",  help="Custom MIDI file path")
    args = ap.parse_args()

    # ---------- B: 環境変数フォールバック ----------
    root = os.path.abspath(
        args.root
        or os.environ.get("PROJECT_ROOT")
        or os.path.join(os.path.dirname(__file__), "..")
    )

    midi_name = args.mid or os.environ.get("MIDI_FILENAME") or "edited_score.mid"
    root = os.path.normpath(root)

    # ---------- 各パス構築 ----------
    exe = os.path.join(root, "Tools", "fluidsynth", "fluidsynth.exe")
    sf2 = args.sf2 or os.path.join(root, "Tools", "fluidsynth","SoundFonts" ,"Arachno.sf2")
    mid = os.path.join(root, "Assets","StreamingAssets", "scores", "edited_score.mid")
    exe = os.path.normpath(exe)
    sf2 = os.path.normpath(sf2)
    mid = os.path.normpath(mid)


    for label, path in [("FluidSynth", exe), ("SoundFont", sf2), ("MIDI", mid)]:
            if not os.path.exists(path):
                print(f"[ERROR] {label} not found: {path}", file=sys.stderr)
                sys.exit(1)

    cmd = [exe, "-i","-n","-a","dsound","-o", f"synth.default-soundfont={sf2}",  mid]
 #   print("[RUN]", " ".join(f'"{x}"' if " " in x else x for x in cmd))
    subprocess.run(cmd, cwd=root)

if __name__ == "__main__":
    main()