# predict_emotion.py
import sys
import json
from pathlib import Path
from typing import Dict, Any

import joblib
import numpy as np
from music21 import converter, note, stream


def _json_ok(obj: Dict[str, Any]) -> None:
    obj["ok"] = True
    print(json.dumps(obj, ensure_ascii=False))


def _json_error(msg: str) -> None:
    print(json.dumps({"ok": False, "error": msg}, ensure_ascii=False))


def extract_features_from_score(sc: stream.Score) -> Dict[str, float]:
    elems = list(sc.recurse().notesAndRests)
    notes = [e for e in elems if isinstance(e, note.Note)]
    rests = [e for e in elems if getattr(e, "isRest", False)]

    if not notes:
        return {
            "note_count": 0.0,
            "rest_ratio": 1.0,
            "pitch_range": 0.0,
            "avg_interval": 0.0,
            "avg_duration": 0.0,
            "density": 0.0,
            "short_note_ratio": 0.0,
        }

    mids = [n.pitch.midi for n in notes if getattr(n, "pitch", None) is not None]
    pitch_range = float(max(mids) - min(mids)) if mids else 0.0

    durs = [float(n.quarterLength) for n in notes if float(n.quarterLength) > 0]
    avg_duration = float(sum(durs) / len(durs)) if durs else 0.0

    intervals = []
    for i in range(1, len(mids)):
        intervals.append(abs(mids[i] - mids[i - 1]))
    avg_interval = float(sum(intervals) / len(intervals)) if intervals else 0.0

    total = len(elems)
    rest_ratio = float(len(rests) / total) if total > 0 else 0.0

    total_quarters = float(getattr(sc.duration, "quarterLength", 0.0) or 0.0)
    density = float(len(notes) / total_quarters) if total_quarters > 0 else float(len(notes))

    short_count = sum(1 for ql in durs if ql in (0.25, 0.5))
    short_note_ratio = float(short_count / len(durs)) if durs else 0.0

    return {
        "note_count": float(len(notes)),
        "rest_ratio": rest_ratio,
        "pitch_range": pitch_range,
        "avg_interval": avg_interval,
        "avg_duration": avg_duration,
        "density": density,
        "short_note_ratio": short_note_ratio,
    }


def main():
    try:
        raw = sys.stdin.read()
        if not raw.strip():
            raise ValueError("no JSON on stdin")
        cmd = json.loads(raw)
    except Exception as e:
        _json_error(f"Json parse error: {e}")
        return

    music_xml = cmd.get("musicXml", "")
    model_path = cmd.get("modelPath", "")

    if not isinstance(music_xml, str) or not music_xml.strip():
        _json_error("musicXml is missing")
        return
    if not isinstance(model_path, str) or not model_path.strip():
        _json_error("modelPath is missing")
        return

    xmlp = Path(music_xml)
    mp = Path(model_path)
    if not xmlp.exists():
        _json_error(f"musicXml not found: {xmlp}")
        return
    if not mp.exists():
        _json_error(f"modelPath not found: {mp}")
        return

    try:
        payload = joblib.load(str(mp))
        labels = payload["labels"]
        feature_names = payload["feature_names"]
        model = payload["model"]
    except Exception as e:
        _json_error(f"Failed to load model: {e}")
        return

    try:
        sc = converter.parse(str(xmlp))
        feats = extract_features_from_score(sc)
        x = np.asarray([[feats[k] for k in feature_names]], dtype=np.float32)
        proba = model.predict_proba(x)[0]
    except Exception as e:
        _json_error(f"Failed to predict: {e}")
        return

    scores = {labels[i]: float(proba[i]) for i in range(len(labels))}
    pred = max(scores.items(), key=lambda kv: kv[1])[0]
    _json_ok({"pred": pred, "scores": scores})


if __name__ == "__main__":
    main()
