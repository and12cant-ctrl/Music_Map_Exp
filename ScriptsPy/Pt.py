import subprocess
import os

# 仮想環境からでもこの相対パスでOK
root = os.path.dirname(os.path.dirname(__file__))
exe = os.path.join(root, "Tools", "fluidsynth", "fluidsynth.exe")
sf2 = os.path.join(root, "Tools", "fluidsynth", "soundfont.sf2")
mid = os.path.join(root, "StreamingAssets", "scores", "edited_score.mid")

subprocess.run([exe, "-i", sf2, mid])