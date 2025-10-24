# test_env.py
import sys
print("Python:", sys.version)
print("Exec  :", sys.executable)
try:
    import music21
    print("music21:", music21.__version__)
except Exception as e:
    print("music21: NOT AVAILABLE ->", e)