class my_class(object):
    pass
import sys
from music21 import converter, chord

def extract_chords(midi_path):
    score = converter.parse(midi_path)
    chords = score.chordify()
    return [str(chord.Chord(c).root().name) for c in chords.recurse().getElementsByClass('Chord')]

def compare_chords(chords1, chords2):
    match = sum(1 for a, b in zip(chords1, chords2) if a == b)
    max_len = max(len(chords1), len(chords2))
    return int((match / max_len) * 100) if max_len > 0 else 0

def main():
    if len(sys.argv) != 3:
        print("Usage: judge_music.py player.mid answer.mid")
        return

    player_path = sys.argv[1]
    answer_path = sys.argv[2]

    player_chords = extract_chords(player_path)
    answer_chords = extract_chords(answer_path)

    score = compare_chords(player_chords, answer_chords)
    print(f"スコア：{score}点")
    print("コメント：コード進行の一致度に基づいた評価です。")

if __name__ == "__main__":
    main()




