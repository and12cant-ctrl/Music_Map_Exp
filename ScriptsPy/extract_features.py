
from __future__ import annotations

import argparse
import csv
import json
import re
from pathlib import Path
from typing import Dict, Any, Optional, List, Tuple

from music21 import converter, stream, note, chord, meter, tempo, key, interval

# -----------------------------
# Basic math utils
# -----------------------------
def safe_float(x: Any, default: float = 0.0) -> float:
    try:
        return float(x)
    except Exception:
        return default

def mean(xs: List[float]) -> float:
    return sum(xs) / len(xs) if xs else 0.0

def std(xs: List[float]) -> float:
    if len(xs) <= 1:
        return 0.0
    m = mean(xs)
    var = sum((x - m) ** 2 for x in xs) / (len(xs) - 1)
    return var ** 0.5

def ratio(num: float, den: float) -> float:
    return (num / den) if den != 0 else 0.0


# -----------------------------
# Parsing helpers
# -----------------------------
def flatten_notes(s: stream.Stream) -> List[note.Note]:
    out: List[note.Note] = []
    for el in s.recurse().notes:
        if isinstance(el, note.Note):
            out.append(el)
    return out

def flatten_onsets(s: stream.Stream) -> List[Tuple[float, Any]]:
    events: List[Tuple[float, Any]] = []
    for el in s.recurse().notes:
        events.append((safe_float(el.offset), el))
    events.sort(key=lambda t: t[0])
    return events

def get_time_signature(sc: stream.Score) -> Tuple[int, int]:
    ts = sc.recurse().getElementsByClass(meter.TimeSignature)
    if ts:
        t = ts[0]
        return int(t.numerator), int(t.denominator)
    return 4, 4

def get_tempo_bpm(sc: stream.Score) -> float:
    tms = sc.recurse().getElementsByClass(tempo.MetronomeMark)
    if tms:
        mm = tms[0]
        return safe_float(mm.number, 0.0)
    return 0.0

def get_bars_len(part: stream.Part) -> int:
    measures = part.getElementsByClass(stream.Measure)
    if measures:
        return len(measures)
    dur_q = safe_float(part.duration.quarterLength, 0.0)
    return int(round(dur_q / 4.0)) if dur_q > 0 else 0

def get_total_duration_quarter(s: stream.Stream) -> float:
    return safe_float(s.duration.quarterLength, 0.0)

def estimate_key(sc: stream.Score) -> Optional[key.Key]:
    try:
        return sc.analyze("key")
    except Exception:
        return None


# -----------------------------
# Filename pairing (MAIN -> ACC)
# -----------------------------
def is_main_file(p: Path) -> bool:
    return "MAIN" in p.stem.upper()

def base_key_from_main(main_path: Path) -> str:
    # 末尾の _MAIN を落とす（大小対応）
    return re.sub(r"(_MAIN)$", "", main_path.stem, flags=re.IGNORECASE).upper()

def resolve_acc_list_from_main(main_path: Path) -> List[Path]:
    folder = main_path.parent
    base_key = base_key_from_main(main_path)

    accs: List[Path] = []
    for p in folder.glob("*.musicxml"):
        su = p.stem.upper()
        if base_key not in su:
            continue
        if "MAIN" in su:
            continue
        # FULLも無視したい場合はここで除外できる
        if "FULL" in su: continue
        accs.append(p)

    accs.sort(key=lambda x: x.stem.upper())

    if not accs:
        return []
    return accs

ACC_KEYS = [
    "bass_mean_midi","bass_range_midi","bass_step_ratio","bass_leap_ratio",
    "chord_event_count_per_bar","mean_chord_size","chord_size_std","polyphony_ratio_time","accomp_density_time",
    "dissonance_ratio","tritone_ratio","minor_third_ratio","major_third_ratio","seventh_chord_ratio","sus_ratio",
    "key_fit_ratio_acc","chromatic_ratio_acc","cadence_strength_score",
    * [f"pc_acc_{i}" for i in range(12)],
]

def aggregate_acc_features_mean(acc_feature_list: List[Dict[str, float]]) -> Dict[str, float]:
    agg: Dict[str, float] = {}
    for k in ACC_KEYS:
        vals = [safe_float(d.get(k, 0.0), 0.0) for d in acc_feature_list]
        agg[k] = mean(vals) if vals else 0.0
    return agg
# -----------------------------
# Feature: MAIN
# -----------------------------
def key_fit_and_chromatic_ratio(notes: List[note.Note], k: Optional[key.Key]) -> Tuple[float, float, float]:
    if not notes:
        return 0.0, 0.0, 0.0

    total = 0
    diat = 0
    accidental = 0

    scale_pitches = set()
    if k is not None:
        try:
            scale_pitches = set(p.name for p in k.getScale().getPitches())
        except Exception:
            scale_pitches = set()

    for n in notes:
        if getattr(n, "pitch", None) is None:
            continue
        total += 1
        pname = n.pitch.name
        if "#" in pname or "b" in pname or "-" in pname:
            accidental += 1
        if scale_pitches and (n.pitch.name in scale_pitches):
            diat += 1

    key_fit = ratio(diat, total) if scale_pitches else 0.0
    chroma = 1.0 - key_fit if scale_pitches else 0.0
    acc_ratio = ratio(accidental, total)
    return key_fit, chroma, acc_ratio

def pitch_class_histogram(notes_list: List[note.Note], prefix: str = "pc_") -> Dict[str, float]:
    """
    Pitch Class histogram (0..11) as ratios (transposition-invariant).
    prefix="pc_"     -> pc_0..pc_11
    prefix="pc_acc_" -> pc_acc_0..pc_acc_11
    """
    counts = [0] * 12
    total = 0
    for n in notes_list:
        p = getattr(n, "pitch", None)
        if p is None:
            continue
        try:
            counts[int(p.pitchClass)] += 1
            total += 1
        except Exception:
            continue

    out: Dict[str, float] = {}
    for i in range(12):
        out[f"{prefix}{i}"] = (counts[i] / total) if total > 0 else 0.0
    return out

def tonic_ending_flag(part: stream.Part, k: Optional[key.Key]) -> float:
    try:
        ns = [n for n in part.flat.recurse().notes if isinstance(n, note.Note)]
        if not ns or k is None:
            return 0.0
        last = ns[-1].pitch.name
        tonic = k.tonic.name
        dom = k.dominant.name if hasattr(k, "dominant") else None
        return 1.0 if (last == tonic or (dom and last == dom)) else 0.0
    except Exception:
        return 0.0

def syncopation_score_simple(part: stream.Part, ts_num: int, ts_den: int) -> float:
    try:
        events = flatten_onsets(part.flat)
        if not events:
            return 0.0
        beat_unit_q = 4.0 / float(ts_den)
        sync = 0
        total = 0
        for off, _el in events:
            total += 1
            rem = (off / beat_unit_q) % 1.0
            if rem != 0.0:
                sync += 1
        return ratio(sync, total)
    except Exception:
        return 0.0

def extract_main_features(main_part: stream.Part, sc_ref: stream.Score) -> Dict[str, float]:
    # ---- context (time signature / key) ----
    ts_num, ts_den = get_time_signature(sc_ref)
    k = estimate_key(sc_ref)

    # ---- notes and durations ----
    ns = flatten_notes(main_part.flat)
    total_dur = get_total_duration_quarter(main_part.flat)

    mids = [float(n.pitch.midi) for n in ns if getattr(n, "pitch", None) is not None]
    durs = [safe_float(n.duration.quarterLength, 0.0) for n in ns]
    pc_main = pitch_class_histogram(ns, prefix="pc_")

    # ---- bars length (quarterLength based) ----
    bar_quarter_len = ts_num * (4.0 / ts_den)  # 4/4->4.0, 6/8->3.0
    bars_len = max(1.0, safe_float(total_dur / bar_quarter_len, 1.0))

    # ---- onset density (unique offsets per bar) ----
    offsets: List[float] = []
    for n in ns:
        try:
            offsets.append(float(n.offset))
        except Exception:
            continue
    unique_onsets = len(set(offsets))
    onset_count_per_bar = unique_onsets / bars_len

    # ---- beat placement features (strongbeat / offbeat) ----
    # Use unique onsets (avoid chord count inflation)
    unique_offsets = sorted(set(offsets))
    if unique_offsets:
        eps = 1e-3  # tolerance for floating point
        strong_hits = 0
        for off in unique_offsets:
            pos_in_bar = off % bar_quarter_len  # 0..bar_quarter_len
            # strong beat: bar start (beat 1)
            if abs(pos_in_bar - 0.0) < eps or abs(pos_in_bar - bar_quarter_len) < eps:
                strong_hits += 1

        strongbeat_hit_ratio = strong_hits / len(unique_offsets)
        offbeat_ratio = (len(unique_offsets) - strong_hits) / len(unique_offsets)
    else:
        strongbeat_hit_ratio = 0.0
        offbeat_ratio = 0.0

    # ---- melodic motion (intervals / direction changes) ----
    intervals_abs: List[float] = []
    directions: List[int] = []
    for i in range(1, len(ns)):
        try:
            semis = interval.Interval(ns[i - 1], ns[i]).semitones
            intervals_abs.append(abs(float(semis)))
            directions.append(1 if semis > 0 else (-1 if semis < 0 else 0))
        except Exception:
            continue

    step = sum(1 for x in intervals_abs if x <= 2)
    leap = sum(1 for x in intervals_abs if x >= 5)
    up = sum(1 for d in directions if d > 0)
    down = sum(1 for d in directions if d < 0)

    flips = 0
    prev = 0
    for d in directions:
        if d == 0:
            continue
        if prev != 0 and d != prev:
            flips += 1
        prev = d
    dir_nonzero = sum(1 for d in directions if d != 0)
    change_rate = ratio(flips, max(1, dir_nonzero))

    # ---- density / duration stats ----
    note_count_per_bar = ratio(len(ns), bars_len)

    short = sum(1 for q in durs if 0 < q <= 0.5)
    short_ratio = ratio(short, len(durs)) if durs else 0.0

    sounding = sum(durs)
    rest_ratio = ratio(max(0.0, total_dur - sounding), total_dur) if total_dur > 0 else 0.0

    # ---- tonal / rhythmic features ----
    key_fit, chroma, acc_ratio = key_fit_and_chromatic_ratio(ns, k)
    tonic_end = tonic_ending_flag(main_part, k)
    sync_score = syncopation_score_simple(main_part, ts_num, ts_den)

    # ---- output ----
    return {
        # pitch (transposition-robust)
        "pitch_std_midi": std(mids),
        "pitch_range_midi": (max(mids) - min(mids)) if mids else 0.0,
        **pc_main,

        # melodic motion
        "interval_abs_mean_semitone": mean(intervals_abs),
        "interval_abs_std_semitone": std(intervals_abs),
        "step_ratio": ratio(step, len(intervals_abs)) if intervals_abs else 0.0,
        "leap_ratio": ratio(leap, len(intervals_abs)) if intervals_abs else 0.0,
        "direction_up_ratio": ratio(up, (up + down)) if (up + down) > 0 else 0.0,
        "direction_change_rate": change_rate,

        # rhythm / density
        "note_count_per_bar": note_count_per_bar,
        "onset_count_per_bar": onset_count_per_bar,
        "dur_mean_quarter": mean(durs),
        "dur_std_quarter": std(durs),
        "short_note_ratio": short_ratio,
        "rest_ratio_time": rest_ratio,
        "syncopation_score": sync_score,

        # tonality
        "key_fit_ratio": key_fit,
        "chromatic_ratio": chroma,
        "accidental_note_ratio": acc_ratio,
        "tonic_ending_flag": tonic_end,

        "strongbeat_hit_ratio": strongbeat_hit_ratio,
        "offbeat_ratio": offbeat_ratio,
    }


# -----------------------------
# Feature: ACC
# -----------------------------
def get_first_part(sc: stream.Score) -> stream.Part:
    if sc.parts:
        return sc.parts[0]
    raise RuntimeError("Score has no parts.")

def extract_bass_line_notes(acc_part: stream.Part) -> List[note.Note]:
    bass: List[note.Note] = []
    for el in acc_part.flat.recurse().notes:
        if isinstance(el, chord.Chord):
            try:
                low = min(el.pitches, key=lambda p: p.midi)
                n = note.Note(low)
                n.duration = el.duration
                bass.append(n)
            except Exception:
                continue
        elif isinstance(el, note.Note):
            bass.append(el)
    return bass

def dissonance_ratio_simple(acc_part: stream.Part) -> float:
    targets = {1, 2, 6, 10, 11}
    chords = [el for el in acc_part.flat.recurse().notes if isinstance(el, chord.Chord)]
    if not chords:
        return 0.0
    dis = 0
    for ch in chords:
        semis = set()
        ps = list(ch.pitches)
        for i in range(len(ps)):
            for j in range(i + 1, len(ps)):
                semis.add(abs(int(ps[j].midi - ps[i].midi)) % 12)
        if semis.intersection(targets):
            dis += 1
    return ratio(dis, len(chords))

def tritone_ratio(acc_part: stream.Part) -> float:
    chords = [el for el in acc_part.flat.recurse().notes if isinstance(el, chord.Chord)]
    if not chords:
        return 0.0
    tri = 0
    for ch in chords:
        ps = list(ch.pitches)
        found = False
        for i in range(len(ps)):
            for j in range(i + 1, len(ps)):
                if abs(int(ps[j].midi - ps[i].midi)) % 12 == 6:
                    found = True
                    break
            if found:
                break
        if found:
            tri += 1
    return ratio(tri, len(chords))

def chord_quality_ratios(acc_part: stream.Part) -> Dict[str, float]:
    chords = [el for el in acc_part.flat.recurse().notes if isinstance(el, chord.Chord)]
    if not chords:
        return {"minor_third_ratio": 0.0, "major_third_ratio": 0.0, "seventh_chord_ratio": 0.0, "sus_ratio": 0.0}

    m3 = M3 = sev = sus = 0
    for ch in chords:
        semis = set()
        ps = list(ch.pitches)
        for i in range(len(ps)):
            for j in range(i + 1, len(ps)):
                semis.add(abs(int(ps[j].midi - ps[i].midi)) % 12)
        if 3 in semis: m3 += 1
        if 4 in semis: M3 += 1
        if 10 in semis or 11 in semis: sev += 1
        if 2 in semis or 5 in semis: sus += 1

    n = len(chords)
    return {
        "minor_third_ratio": ratio(m3, n),
        "major_third_ratio": ratio(M3, n),
        "seventh_chord_ratio": ratio(sev, n),
        "sus_ratio": ratio(sus, n),
    }

def cadence_strength_score_simple(acc_part: stream.Part, kref: Optional[key.Key]) -> float:
    if kref is None:
        return 0.0
    chords = [el for el in acc_part.flat.recurse().notes if isinstance(el, chord.Chord)]
    if not chords:
        return 0.0
    last = chords[-1]
    try:
        root = last.root().name
    except Exception:
        root = last.pitches[0].name if last.pitches else ""
    tonic = kref.tonic.name
    dom = kref.dominant.name if hasattr(kref, "dominant") else ""
    if root == tonic:
        return 1.0
    if root == dom:
        return 0.7
    return 0.0

def extract_acc_features(acc_score: stream.Score, key_ref_score: stream.Score) -> Dict[str, float]:
    k = estimate_key(key_ref_score)
    acc_part = get_first_part(acc_score)

    chord_events = [el for el in acc_part.flat.recurse().notes if isinstance(el, chord.Chord)]
    bars_len = max(1, get_bars_len(acc_part))
    chord_event_count_per_bar = ratio(len(chord_events), bars_len)

    chord_sizes = [float(len(ch.pitches)) for ch in chord_events]
    pc_counts = [0] * 12
    pc_total = 0
    for ch in chord_events:
        # ch is music21.chord.Chord
        for p in ch.pitches:
            try:
                pc_counts[int(p.pitchClass)] += 1
                pc_total += 1
            except Exception:
                continue
    pc_acc = {f"pc_acc_{i}": (pc_counts[i] / pc_total) if pc_total > 0 else 0.0 for i in range(12)}
    total_dur = get_total_duration_quarter(acc_part.flat)

    poly_time = sum(safe_float(ch.duration.quarterLength, 0.0) for ch in chord_events if len(ch.pitches) >= 2)
    polyphony_ratio_time = ratio(poly_time, total_dur) if total_dur > 0 else 0.0

    sounding = sum(safe_float(el.duration.quarterLength, 0.0) for el in acc_part.flat.recurse().notes)
    accomp_density_time = ratio(min(sounding, total_dur), total_dur) if total_dur > 0 else 0.0

    bass_notes = extract_bass_line_notes(acc_part)
    bass_mids = [float(n.pitch.midi) for n in bass_notes if getattr(n, "pitch", None) is not None]
    bass_intervals: List[float] = []
    for i in range(1, len(bass_notes)):
        try:
            bass_intervals.append(abs(float(interval.Interval(bass_notes[i - 1], bass_notes[i]).semitones)))
        except Exception:
            continue
    bass_step = sum(1 for x in bass_intervals if x <= 2)
    bass_leap = sum(1 for x in bass_intervals if x >= 5)

    acc_notes_for_key: List[note.Note] = []
    for el in acc_part.flat.recurse().notes:
        if isinstance(el, chord.Chord):
            for p in el.pitches:
                nn = note.Note(p)
                nn.duration = el.duration
                acc_notes_for_key.append(nn)
        elif isinstance(el, note.Note):
            acc_notes_for_key.append(el)

    key_fit, chroma, _ = key_fit_and_chromatic_ratio(acc_notes_for_key, k)
    dis = dissonance_ratio_simple(acc_part)
    tri = tritone_ratio(acc_part)
    quals = chord_quality_ratios(acc_part)
    cad = cadence_strength_score_simple(acc_part, k)

    return {
        "bass_mean_midi": mean(bass_mids),
        "bass_range_midi": (max(bass_mids) - min(bass_mids)) if bass_mids else 0.0,
        "bass_step_ratio": ratio(bass_step, len(bass_intervals)) if bass_intervals else 0.0,
        "bass_leap_ratio": ratio(bass_leap, len(bass_intervals)) if bass_intervals else 0.0,
        "chord_event_count_per_bar": chord_event_count_per_bar,
        "mean_chord_size": mean(chord_sizes),
        "chord_size_std": std(chord_sizes),
        "polyphony_ratio_time": polyphony_ratio_time,
        "accomp_density_time": accomp_density_time,
        "dissonance_ratio": dis,
        "tritone_ratio": tri,
        **pc_acc,
        **quals,
        "key_fit_ratio_acc": key_fit,
        "chromatic_ratio_acc": chroma,
        "cadence_strength_score": cad,
    }


# -----------------------------
# Meta
# -----------------------------
def pickup_flag_simple(main_part: stream.Part) -> float:
    try:
        m0 = main_part.getElementsByClass(stream.Measure)[0]
        ts = m0.getContextByClass(meter.TimeSignature)
        if ts is None:
            return 0.0
        bar_len = ts.barDuration.quarterLength
        mlen = safe_float(m0.duration.quarterLength, 0.0)
        return 1.0 if (mlen > 0 and mlen < bar_len * 0.9) else 0.0
    except Exception:
        return 0.0

def extract_common_meta(sc_main: stream.Score, main_part: stream.Part) -> Dict[str, float]:
    ts_num, ts_den = get_time_signature(sc_main)
    bpm = get_tempo_bpm(sc_main)
    bars_len = get_bars_len(main_part)
    total_q = get_total_duration_quarter(main_part.flat)
    pickup = pickup_flag_simple(main_part)
    return {
        "time_signature_num": float(ts_num),
        "time_signature_den": float(ts_den),
        "tempo_bpm_est": float(bpm),
        "bars_len": float(bars_len),
        "total_duration_quarter": float(total_q),
        "pickup_flag": float(pickup),
    }


# -----------------------------
# CSV columns
# -----------------------------
FINAL_COLUMNS = [
    # basics
    "time_signature_num","time_signature_den","tempo_bpm_est","bars_len","total_duration_quarter","pickup_flag",

    # MAIN (melody) - transposition-invariant + robust pitch stats
    "pitch_std_midi","pitch_range_midi",
    *[f"pc_{i}" for i in range(12)],
    "interval_abs_mean_semitone","interval_abs_std_semitone",
    "step_ratio","leap_ratio","direction_up_ratio","direction_change_rate",
    "note_count_per_bar","onset_count_per_bar",
    "dur_mean_quarter","dur_std_quarter","short_note_ratio","rest_ratio_time","syncopation_score",
    "key_fit_ratio","chromatic_ratio","accidental_note_ratio","tonic_ending_flag",

    # ACC (accompaniment) aggregated over matching ACC files
    "bass_mean_midi","bass_range_midi","bass_step_ratio","bass_leap_ratio",
    "chord_event_count_per_bar","mean_chord_size","chord_size_std",
    "polyphony_ratio_time","accomp_density_time",
    "dissonance_ratio","tritone_ratio","minor_third_ratio","major_third_ratio",
    "seventh_chord_ratio","sus_ratio",
    "key_fit_ratio_acc",
    *[f"pc_acc_{i}" for i in range(12)],
    "chromatic_ratio_acc","cadence_strength_score",
    "strongbeat_hit_ratio", "offbeat_ratio"
]


def write_json(path: Path, data: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")

def append_csv(path: Path, row: Dict[str, Any]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    exists = path.exists()
    with path.open("a", newline="", encoding="utf-8") as f:
        w = csv.DictWriter(f, fieldnames=FINAL_COLUMNS)
        if not exists:
            w.writeheader()
        w.writerow({c: row.get(c, 0.0) for c in FINAL_COLUMNS})


# -----------------------------
# Main
# -----------------------------
def extract_all(main_path: Path) -> Dict[str, Any]:
    if not is_main_file(main_path):
        raise ValueError("Input must be a MAIN file (stem contains 'MAIN').")
    sc_main: stream.Score = converter.parse(str(main_path))

    if not sc_main.parts:
        raise RuntimeError(f"MAIN has no parts: {main_path}")
    main_part = sc_main.parts[0]

    common = extract_common_meta(sc_main, main_part)
    main_feats = extract_main_features(main_part, sc_main)

    acc_paths = resolve_acc_list_from_main(main_path)
    acc_feat_list: List[Dict[str, float]] = []
    for ap in acc_paths:
        sc_acc = converter.parse(str(ap))
        acc_feat_list.append(extract_acc_features(sc_acc, sc_main))

    acc_agg = aggregate_acc_features_mean(acc_feat_list)

    out: Dict[str, Any] = {
        "source_main": str(main_path),
        "acc_count": float(len(acc_paths)),
        "main_partName": getattr(main_part, "partName", "") or "",
        **common,
        **main_feats,
        **acc_agg
    }
    for c in FINAL_COLUMNS:
        out.setdefault(c, 0.0)
    return out

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("main", type=str, help="input MAIN MusicXML path")
    ap.add_argument("--out_json", type=str, default="")
    ap.add_argument("--out_csv", type=str, default="")
    args = ap.parse_args()

    out = extract_all(Path(args.main))

    if args.out_json:
        write_json(Path(args.out_json), out)
    if args.out_csv:
        append_csv(Path(args.out_csv), out)
        print(f"CSV_OK\t{args.out_csv}")
    else:
        print(json.dumps(out, ensure_ascii=False))

    print(json.dumps(out, ensure_ascii=False))

if __name__ == "__main__":
    main()
