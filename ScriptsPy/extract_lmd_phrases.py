
"""
Lakh MIDI Dataset から 2〜4 小節の単一パートフレーズを大量抽出するスクリプト。

・入力：LMD の .mid ファイルが入ったディレクトリ（例: LMD-clean のルート）
・出力：2〜4小節のフレーズを MusicXML 形式で保存したディレクトリ

処理の流れ：
  1. 再帰的に .mid を探索
  2. 各ファイルを music21 で parse
  3. 「ノート数が最大のパート」をメロディパートとして選択
  4. 拍子が 4/4, 3/4, 2/2 以外ならスキップ
  5. 2〜4小節の連続フレーズをすべて候補とし、音価チェックを通ったものだけ採用
  6. 各MIDIファイルあたり最大 N フレーズまでを書き出し

"""

import argparse
import random
import sys
import json
import re
from pathlib import Path
from statistics import mean
from music21 import converter, meter, note, stream


# 許可する拍子
ALLOWED_TS = {"4/4", "3/4", "2/2"}

# 許可する音価（quarterLength）
ALLOWED_DURATIONS = {4.0, 2.0, 1.0, 0.5
                     ,0.25,1.5,3.0}

LENGTH_BONUS = {2: 1.00, 3: 1.15, 4: 1.30}


def _json_error(msg):
    print(json.dumps({"ok": False, "error": msg}))
# Partがある場合は、Partそのもの＋(可能なら) Voice分解したPart(voicesToParts) を候補にする
# avg_pitch（高め） + avg_duration（長め） + density（低め）で旋律候補を選ぶ
def pick_melody_part(score: stream.Score) -> stream.Part | stream.Stream:
    if not getattr(score, "parts", None) and len(score.parts) > 0:
        return score

    candidates: list[stream.Stream] = []

    for p in score.parts:
        candidates.append(p)

        try:
            vp = p.voicesToParts()
            if vp:
                candidates.extend(vp)
        except Exception:
            pass

    feats = []
    for cand in candidates:
        stats = calc_phrase_stats(cand)
        if stats["note_count"] <= 0:
            continue
        ap = _avg_pitch_midi(cand)
        feats.append((cand,ap,stats["avg_duration"],stats["density"]))

    if not feats:
        return score

    pitches = [x[1] for x in feats]
    durs    = [x[2] for x in feats]
    pmin,pmax = min(pitches),max(pitches)
    dmin,dmax = min(durs),max(durs)

    def norm(x,lo,hi):
        return 0.5 if hi <= lo else (x-lo)/(hi - lo)

    best_stream = cand
    best_s = -1e9
    for cand, ap ,ad,_den in feats:
        s = 0.6 * norm(ap,pmin,pmax) + 0.4 * norm(ad , dmin,dmax)
        if s > best_s:
            best_s = s
            best_stream = cand
    return best_stream

# ストリーム内の Note の平均音高と平均音価を返す（未使用補助関数）。
def avg_pitch_duration(p: stream.Stream):
    notes = [n for n in p.recurse().notes if isinstance(n,note.Note)]
    if not notes:
        return None
    avg_pitch = mean(n.pitch.midi for n in notes if n.pitch is not None)
    avg_dur = sum(float(n.quarterLength) for n in notes) / len (notes)
    return avg_pitch,avg_dur

# Note の pitch.midi の平均を返す（主旋律選別に使用）。
def _avg_pitch_midi(s: stream.Stream) -> float:
    ns = [n for n in s.recurse().notes if isinstance(n, note.Note) and getattr(n, "pitch", None) is not None]
    if not ns:
        return 0.0
    return float(mean(n.pitch.midi for n in ns))

# ファイル/フォルダ名に使えない文字を _ に置換して安全化。
def safeFolder(name:str) -> str:
     return re.sub(r'[<>:"/\\|?*\x00-\x1f]',"_",name).strip().strip(".")

# フレーズ内の音価が「許可セット中心」かを判定してフィルタする。
def phrase_has_allowed_durations(phrase: stream.Stream,min_noteRatio:float = 0.8) -> bool:
    elements = list(phrase.recurse().notesAndRests)
    notes = [el for el in elements if isinstance(el,note.Note)]
    if not notes:
        return False
    ok = 0

    for no in notes:
        ql = float(no.quarterLength)
        if ql <= 0:
            return False
        if ql in ALLOWED_DURATIONS:
            ok += 1

    ratio = ok /len(notes)
    return ratio >= min_noteRatio
# 候補の中から「できるだけ長い（4→3→2）」ものを優先しつつ、指定スコア最大を選ぶ。
def pick_best_by_len(cands, score_idx: int):
    # score_idx: energy=4, stability=5
    for L in (4, 3, 2):
        pool = [c for c in cands if c[2] == L]
        if pool:
            return max(pool, key=lambda x: x[score_idx])
    return None

# Part 内の Voice から「旋律っぽい Voice」を選ぶ。
def safe_extract_melody_voice(part) -> stream.Stream:
    candidates = []
    try:
        voices = list(part.getElementsByClass(stream.Voice))
    except Exception:
        voices = []

    for v in voices:
        ns = [n for n in v.notes if isinstance(n, note.Note) and getattr(n, "pitch", None) is not None]
        if not ns:
            continue

        avg_pitch = sum(n.pitch.midi for n in ns) / len(ns)
        continuity = sum(float(n.quarterLength) for n in ns)
        # Voice内要素に対する Note の比率（休符・他要素が多いと下がる）
        single_ratio = len(ns) / (len(list(v.notesAndRests)) + 1)

        score = avg_pitch * 0.4 + continuity * 0.4 + single_ratio * 0.2
        candidates.append((score, v))

    if not candidates:
        return part

    candidates.sort(key=lambda x: x[0], reverse=True)
    return candidates[0][1]

def safePartName(p,idx:int) -> str:
    name = (getattr(p, "partName", None) or getattr(p, "id", None) or f"part{idx}")
    return safeFolder(str(name))

def other_parts(score,main_part):
    outs = []
    for i,p in enumerate(score.parts):
        if p is main_part:
            continue
        outs.append((i,p))
    return outs

def fullscore_slice(score: stream.Score,start_num: int,end_num:int) -> stream.Score | None:
    out = stream.Score()
    ok = False
    for p in score.parts:
        try:
            ph = p.measures(start_num,end_num)
            if ph is None:
                continue
            st = calc_phrase_stats(ph)
            if st["note_count"]<= 0:
                continue
            out.insert(0,ph)
            ok = True
        except Exception:
            continue
    return out if ok else None

def extract_phrases_from_file(
    midi_path: Path,
    output_dir: Path,
    min_measures: int = 2,
    max_measures: int = 4,
    max_phrases_per_song: int = 50,
    other: bool = True,
) -> int:
    """
    1つの MIDI ファイルから 2〜4小節フレーズを抽出し、MusicXML で出力する。
    追加仕様：イントロ（冒頭4小節）も必ず保存する（重複は除外）。
    戻り値：保存したフレーズ数, 保存したファイルパスのリスト
    """
    try:
        score = converter.parse(str(midi_path))
    except Exception as e:
        print(f"[ERROR] parse failed: {midi_path} ({e})", file=sys.stderr)
        return 0, []

    part = pick_melody_part(score)

    if is_classical_like(part):
        part = safe_extract_melody_voice(part)

    # 拍子チェック
    ts = part.recurse().getElementsByClass(meter.TimeSignature).first()
    if ts is None:
        print(f"[INFO] No TimeSignature, skip: {midi_path}", file=sys.stderr)
        return 0, []

    ts_str = ts.ratioString  # "4/4" など
    if ts_str not in ALLOWED_TS:
        print(f"[INFO] TS not allowed ({ts_str},skip: {midi_path}", file=sys.stderr)
        return 0, []
    safe_ts = ts_str.replace("/", "_")

    # 小節一覧を取得
    measures = list(part.getElementsByClass("Measure"))
    if not measures:
        print(f"[INFO] No Measure extracted, skip: {midi_path}", file=sys.stderr)
        return 0, []

    numbers = [mea.number for mea in measures if mea.number is not None]
    if not numbers:
        return 0, []
    last_number = max(numbers)

    # 2〜4小節のすべての連続フレーズを候補にする
    candidates: list[tuple[int, int, int, stream.Stream, float, float]] = []

    for length in range(min_measures, max_measures + 1):
        for start_num in range(1, last_number - length + 2):
            end_num = start_num + length - 1
            phrase = part.measures(start_num, end_num)
            if phrase is None:
                continue
            if phrase_has_allowed_durations(phrase):
                stats = calc_phrase_stats(phrase)
                e = score_energy(stats) * LENGTH_BONUS.get(length, 1.0)
                s = score_stability(stats) * LENGTH_BONUS.get(length, 1.0)
                candidates.append((start_num, end_num, length, phrase, e, s))

    if not candidates:
        # ここで「イントロだけは出したい」なら、intro保存だけは続行する実装も可能
        return 0, []

    best_energy = pick_best_by_len(candidates, 4)  # energy score
    best_stable = pick_best_by_len(candidates, 5)  # stability score

    selected = [best_energy]
    if (best_stable[0], best_stable[1]) != (best_energy[0], best_energy[1]):
        selected.append(best_stable)

    selected = selected[:max_phrases_per_song]

    output_dir.mkdir(parents=True, exist_ok=True)

    saved = 0
    outputs: list[str] = []
    stem = midi_path.stem
    song_dir = output_dir / safeFolder(stem)
    song_dir.mkdir(parents=True, exist_ok=True)

    intro_dir = song_dir /"Intro"
    chorus_dir = song_dir / f"Chorus"
    stable_dir = song_dir / f"Stable"

    intro_dir.mkdir(exist_ok=True)
    chorus_dir.mkdir(exist_ok=True)
    stable_dir.mkdir(exist_ok=True)


    intro_len = 4
    intro_end = min(intro_len, last_number)

    # 「最低2小節」はあなたの抽出方針(2〜4小節)に合わせたガード
    if intro_end >= 2:
        intro_phrase = part.measures(1, intro_end)

        if intro_phrase is not None:
            intro_stats = calc_phrase_stats(intro_phrase)

            # イントロは「必ず抜きたい」が目的なので、
            # durationフィルタで落としすぎないよう note_count > 0 を最低条件にするのがおすすめ
            if intro_stats["note_count"] > 0:
                intro_e = score_energy(intro_stats) * LENGTH_BONUS.get(intro_end, 1.0)
                intro_s = score_stability(intro_stats) * LENGTH_BONUS.get(intro_end, 1.0)

                intro_base = f"{stem}_INTRO_m1-{intro_end}_ts{safe_ts}_E{intro_e:.3f}_S{intro_s:.3f}"
                intro_main = intro_dir / f"{intro_base}_MAIN.musicxml"
                full_intro = fullscore_slice(score, 1, intro_end)
                if full_intro is not None:
                    out_full = intro_dir / f"{intro_base}_FULL.musicxml"
                try:
                    full_intro.write("musicxml", fp=str(out_full))
                    saved += 1
                    outputs.append(str(out_full))
                except Exception as e:
                    print(f"[ERROR] intro full write failed: {out_full} ({e})", file=sys.stderr)
                try:
                    intro_phrase.write("musicxml", fp=str(intro_main))
                    saved += 1
                    outputs.append(str(intro_main))
                except Exception as e:
                    print(f"[ERROR] intro write failed: {intro_main} ({e})", file=sys.stderr)

                # イントロの伴奏(Other parts)も同区間で保存
                if other:
                    for pi, p in other_parts(score, part):
                        try:
                            ph = p.measures(1, intro_end)
                            if ph is None:
                                continue
                            st = calc_phrase_stats(ph)
                            if st["note_count"] <= 0:
                                continue

                            pname = safePartName(p, pi)
                            intro_p = intro_dir / f"{intro_base}_P{pi:02d}_{pname}.musicxml"
                            ph.write("musicxml", fp=str(intro_p))
                            saved += 1
                            outputs.append(str(intro_p))
                        except Exception as e:
                            print(f"[WARN] intro other part write failed: {stem} P{pi} ({e})", file=sys.stderr)

                # 通常抽出(selected)と同じ区間があれば重複保存しない
                selected = [
                    x for x in selected
                    if not (x[0] == 1 and x[1] == intro_end)
                ]

    # =========================
    # 通常の selected を保存
    # =========================
    for idx, (start_num, end_num, length, phrase, e_score, s_score) in enumerate(selected):

            # どの役割か判定
        if (start_num, end_num) == (best_energy[0], best_energy[1]):
            role = "Chorus"
            target_dir = chorus_dir
        else:
            role = "Stable"
            target_dir = stable_dir

        out_base = f"{stem}_{role}_m{start_num}-{end_num}_ts{safe_ts}_E{e_score:.3f}_S{s_score:.3f}_{idx:03d}"
        out_main = target_dir / f"{out_base}_MAIN.musicxml"
# --- 追加：全パート入り（パート分けしてない版）も保存 ---
        full_score = fullscore_slice(score, start_num, end_num)
        if full_score is not None:
            out_full = target_dir / f"{out_base}_FULL.musicxml"
            try:
                full_score.write("musicxml", fp=str(out_full))
                saved += 1
                outputs.append(str(out_full))
            except Exception as e:
                print(f"[ERROR] full write failed: {out_full} ({e})", file=sys.stderr)

        try:
            phrase.write("musicxml", fp=str(out_main))
            saved += 1
            outputs.append(str(out_main))
        except Exception as e:
            print(f"[ERROR] write failed: {out_main} ({e})", file=sys.stderr)

        if not other:
            continue

        others = other_parts(score, part)
        for pi, p in others:
            try:
                ph = p.measures(start_num, end_num)
                if ph is None:
                    continue
                st = calc_phrase_stats(ph)
                if st["note_count"] <= 0:
                    continue

                pname = safePartName(p, pi)
                out_p = target_dir / f"{out_base}_P{pi:02d}_{pname}.musicxml"
                ph.write("musicxml", fp=str(out_p))
                saved += 1
                outputs.append(str(out_p))
            except Exception as e:
                print(f"[WARN] other part write failed: {stem} P{pi} ({e})", file=sys.stderr)

    return saved, outputs

# クラシックかどうかの判定
def is_classical_like(part: stream.Stream,min_voices: int = 2) -> bool:
    try:
        voices = list(part.getElementsByClass(stream.Voice))
        return len(voices) >= min_voices
    except Exception:
        return False

def _clamp01(x: float) -> float:
    return 0.0 if x < 0.0 else 1.0 if x > 1.0 else x

def _norm(x: float, lo: float, hi: float) -> float:
    if hi <= lo:
        return 0.0
    return _clamp01((x - lo) / (hi - lo))

def calc_phrase_stats(phrase: stream.Stream) -> dict:
    """
    2〜4小節 phrase から簡易統計を取る（サビ候補スコア用）
    """
    elems = list(phrase.recurse().notesAndRests)
    notes = [e for e in elems if isinstance(e, note.Note)]
    rests = [e for e in elems if e.isRest]

    if not notes:
        return {
            "note_count": 0,
            "rest_ratio": 1.0,
            "pitch_range": 0.0,
            "avg_interval": 0.0,
            "avg_duration": 0.0,
            "density": 0.0,
        }

    # pitches
    mids = [n.pitch.midi for n in notes if getattr(n, "pitch", None) is not None]
    pitch_range = float(max(mids) - min(mids)) if mids else 0.0

    # durations (notes only)
    durs = [float(n.quarterLength) for n in notes]
    avg_duration = sum(durs) / len(durs) if durs else 0.0

    # avg interval (semitones) between consecutive notes
    # 順序は stream 順に依存（十分実用）
    intervals = []
    for i in range(1, len(mids)):
        intervals.append(abs(mids[i] - mids[i - 1]))
    avg_interval = (sum(intervals) / len(intervals)) if intervals else 0.0

    total = len(elems)
    rest_ratio = (len(rests) / total) if total > 0 else 0.0

    # note density: notes per quarter length
    total_quarters = float(getattr(phrase.duration, "quarterLength", 0.0) or 0.0)
    density = (len(notes) / total_quarters) if total_quarters > 0 else float(len(notes))

    return {
        "note_count": len(notes),
        "rest_ratio": float(rest_ratio),
        "pitch_range": float(pitch_range),
        "avg_interval": float(avg_interval),
        "avg_duration": float(avg_duration),
        "density": float(density),
    }

def score_energy(stats: dict) -> float:
    """
    fear/anxiety に強い「盛り上がり」スコア（大きいほどサビ候補）
    """
    # 正規化の目安（LMDでは幅があるのでゆるめ）
    n_pitch_range = _norm(stats["pitch_range"], 0.0, 24.0)   # 2オクターブで1.0
    n_avg_interval = _norm(stats["avg_interval"], 0.0, 12.0) # 1オクターブで1.0
    n_density = _norm(stats["density"], 0.0, 4.0)            # 1拍(quarter)あたり最大4音くらいで1.0

    return 0.45 * n_pitch_range + 0.35 * n_density + 0.20 * n_avg_interval

def score_stability(stats: dict) -> float:
    """
    trust/sad に強い「安定」スコア（大きいほどサビ候補）
    """
    n_avg_duration = _norm(stats["avg_duration"], 0.25, 4.0) # 16分〜全音符
    n_pitch_range = _norm(stats["pitch_range"], 0.0, 24.0)
    n_avg_interval = _norm(stats["avg_interval"], 0.0, 12.0)
    n_rest_ratio = _norm(stats["rest_ratio"], 0.0, 1.0)

    # 「安定」は、音価長め + 跳躍少なめ + 音域狭め + 休符少なめ(※多すぎは不安寄りになりがち) を好む
    return (
        0.40 * n_avg_duration +
        0.25 * (1.0 - n_avg_interval) +
        0.20 * (1.0 - n_pitch_range) +
        0.15 * (1.0 - n_rest_ratio)
    )

def main():

    try:
        raw = sys.stdin.read()
        if not raw.strip():
            raise ValueError("no JSON on stdin")
        cmd = json.loads(raw)
    except Exception as e :
        _json_error("outputDir is missing")
        return

    # 必須パラメータ
    midi_path = Path(cmd.get("midiPath", ""))
    output_dir_str = cmd.get("outputDir", "")


    if not isinstance(output_dir_str,str) or not output_dir_str.strip():
        _json_error("outputDir is missing")
        return

    output_dir = Path(output_dir_str)
    midi_path_str = cmd.get("midiPath", "")
    input_root_str = cmd.get("inputRoot", "")

    # オプション（来てなければデフォルト）
    min_measures = int(cmd.get("minMeasures", 2))
    max_measures = int(cmd.get("maxMeasures", 4))
    max_phrases_per_song = int(cmd.get("maxPhrasesPerSong", 50))
    export_others = bool(cmd.get("exportOtherParts", True))

    max_songs = cmd.get("maxSongs", None)
    if max_songs is not None:
        max_songs = int(max_songs)

    midi_files: list[Path] = []

    if isinstance(midi_path_str,str) and midi_path_str.strip():
        p = Path(midi_path_str)
        if not p.exists() or p.is_dir():
            _json_error(f"Invalid MIDI path: {p}")
            return
        midi_files = [p]

    elif isinstance(input_root_str,str) and input_root_str.strip():
        root = Path(input_root_str)
        if not root.exists() or not root.is_dir():
            _json_error(f"Invalid inputRoot: {root}")
            return
        midi_files = [p for p in root.rglob("*") if p.suffix.lower() in (".mid", ".midi")]
        midi_files.sort()
        if max_songs is not None:
            midi_files = midi_files[:max_songs]

    else:
        _json_error("midiPath or inputRoot is required")
        return

    total_saved = 0
    all_outputs: list[str] = []
    per_song = []

    for mf in midi_files:
        saved, outputs = extract_phrases_from_file(
            midi_path=mf,
            output_dir=output_dir,
            min_measures=min_measures,
            max_measures=max_measures,
            max_phrases_per_song=max_phrases_per_song,
        )
        total_saved += saved
        all_outputs.extend(outputs)
        per_song.append({"midi": str(mf), "savedCount": saved})

    resp = {
        "ok": True,
        "totalSongs": len(midi_files),
        "savedCount": total_saved,
        "outputFiles": all_outputs,      # 既存互換
        "perSong": per_song,             # 追加情報（必要ならUnity側で受ける）
    }
    print(json.dumps(resp, ensure_ascii=False))

if __name__ == "__main__":
    main()
