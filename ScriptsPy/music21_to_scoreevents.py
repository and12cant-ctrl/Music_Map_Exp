# music21_to_scoreevents.py

import sys
import json
from music21 import converter, stream, note, chord

# ========= 設定 =========

PART_NAMES = ["A", "B", "C", "D"]
VOICE_NAMES = {
    1: "Up",
    2: "Middle",
    3: "Down"
}

# ========= util =========

def voice_number_from_context(el):
    v = el.getContextByClass(stream.Voice)
    if v is None:
        return 1
    try:
        return int(v.id)
    except:
        return 1


def pitch_to_string(p):
    s = p.nameWithOctave  # "E-5", "B--3", "F#4"
    s = s.replace("---", "bbb").replace("--", "bb").replace("-", "b")
    return p.nameWithOctave


# ========= main =========

def main():

    if len(sys.argv) < 2:
        print("usage: script.py music.xml")
        sys.exit(1)

    xml_path = sys.argv[1]

    score = converter.parse(xml_path)

    # beatsPerBar（Unityに渡す）
    ts = score.recurse().getElementsByClass("TimeSignature")[0]
    beats_per_bar = float(ts.numerator)

    streams = score.parts

    events = []

    for si, p in enumerate(streams):

        part_name = PART_NAMES[si] if si < len(PART_NAMES) else "A"

        # ⭐ Measure基準で回す（これが最重要）
        for m in p.recurse().getElementsByClass(stream.Measure):

            if m.number is None:
                continue

            bar = int(m.number)

            # ⭐ Measure内のnotesAndRestsを使う
            for el in m.notesAndRests:

                # 小節内offset（これなら負にならない）
                start_in_bar = float(el.offset)

                dur = float(el.quarterLength)

                vno = voice_number_from_context(el)
                voice_name = VOICE_NAMES.get(vno, "Up")

                # ---- REST ----
                if isinstance(el, note.Rest):

                    events.append({
                        "part": part_name,
                        "voice": voice_name,
                        "pitch": "REST",
                        "duration": dur,
                        "bar": bar,
                        "start": start_in_bar
                    })

                # ---- NOTE ----
                elif isinstance(el, note.Note):

                    # ⭐ tie継続/終端は除外（開始だけ欲しい場合）
                    if el.tie is not None and el.tie.type in ("stop", "continue"):
                        continue

                    events.append({
                        "part": part_name,
                        "voice": voice_name,
                        "pitch": pitch_to_string(el.pitch),
                        "duration": dur,
                        "bar": bar,
                        "start": start_in_bar
                    })

                # ---- CHORD ----
                elif isinstance(el, chord.Chord):

                    for pch in el.pitches:

                        events.append({
                            "part": part_name,
                            "voice": voice_name,
                            "pitch": pitch_to_string(pch),
                            "duration": dur,
                            "bar": bar,
                            "start": start_in_bar
                        })

    payload = {
        "beatsPerBar": beats_per_bar,
        "events": events
    }

    print(json.dumps(payload, ensure_ascii=False))


if __name__ == "__main__":
    main()
