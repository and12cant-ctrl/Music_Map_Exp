import json
import sys
from music21 import converter, note, chord, stream, meter

# Unityの Part/voice enum に合わせて文字列で返す（Unity側でEnum.Parseできる）
PART_NAMES = ["A", "B", "C", "D"]
VOICE_NAMES = {
    1: "Up",
    2: "Middle",
    3: "Down",
}

def get_beats_per_bar(score) -> float:
    # まず全体のTimeSignatureを探す
    ts = score.recurse().getElementsByClass(meter.TimeSignature).first()
    if ts is None:
        return 4.0
    # music21は (分子/分母) を持つ。quarterLengthベースの小節長は ts.barDuration.quarterLength
    return float(ts.barDuration.quarterLength)

def iter_part_like_streams(score):
    """
    Piano譜などで PartStaff がある場合はそれを優先。
    無い場合は通常の part をそのまま扱う。
    """
    parts = list(score.parts)
    # PartStaffが存在するケース（ピアノ二段譜など）
    partstaffs = list(score.recurse().getElementsByClass(stream.PartStaff))
    if partstaffs:
        # score内のPartStaffを出現順に採用（概ね上段→下段）
        return partstaffs
    return parts

def voice_number_from_context(el) -> int:
    v = el.getContextByClass(stream.Voice)
    if v is None:
        return 1
    # Voice.id が "1" "2" だったりする
    try:
        return int(v.id)
    except Exception:
        return 1

def pitch_to_string(n: note.Note) -> str:
    # "C#4" 形式で返す（UnityのPitchStringToMidiが読める）
    return n.pitch.nameWithOctave  # 例: C#4, Bb3

def main():
    if len(sys.argv) < 2:
        print(json.dumps({"error": "Usage: python music21_to_scoreevents.py <musicxml_path>"}))
        sys.exit(1)

    path = sys.argv[1]
    score = converter.parse(path)

    beats_per_bar = get_beats_per_bar(score)

    events = []
    streams = iter_part_like_streams(score)

    for si, p in enumerate(streams):
        part_name = PART_NAMES[si] if si < len(PART_NAMES) else "A"

        # notesAndRests で Restも拾える（Unity側でskipRestsできる）
        for el in p.recurse().notesAndRests:
            m = el.getContextByClass(stream.Measure)
            if m is None or m.number is None:
                # 小節が取れない要素は無視（念のため）
                continue

            bar = int(m.number)
            start_in_bar = float(el.offset - m.offset)   # 小節内 start（拍）
            dur = float(el.quarterLength)                # 長さ（拍）

            vno = voice_number_from_context(el)
            voice_name = VOICE_NAMES.get(vno, "Up")

            if isinstance(el, note.Rest):
                pitch = "REST"
                events.append({
                    "part": part_name,
                    "voice": voice_name,
                    "pitch": pitch,
                    "duration": dur,
                    "bar": bar,
                    "start": start_in_bar,
                })

            elif isinstance(el, note.Note):
                pitch = pitch_to_string(el)
                events.append({
                    "part": part_name,
                    "voice": voice_name,
                    "pitch": pitch,
                    "duration": dur,
                    "bar": bar,
                    "start": start_in_bar,
                })

            elif isinstance(el, chord.Chord):
                # 和音は同じstart/durで複数Noteに展開
                for n in el.notes:
                    pitch = pitch_to_string(n)
                    events.append({
                        "part": part_name,
                        "voice": voice_name,
                        "pitch": pitch,
                        "duration": dur,
                        "bar": bar,
                        "start": start_in_bar,
                    })

    out = {
        "beatsPerBar": beats_per_bar,
        "events": events
    }
    print(json.dumps(out, ensure_ascii=False))

if __name__ == "__main__":
    main()
