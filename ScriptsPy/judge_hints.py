# judge_hints.py
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any, Dict, List, Tuple

import numpy as np

from emotion_common import load_meta, predict_with_saved_model, FINAL_COLUMNS


def _read_json_from_file(path: Path) -> Dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def _read_json_from_stdin() -> Dict[str, Any]:
    raw = sys.stdin.read()
    if not raw.strip():
        raise ValueError("stdin が空です。")
    return json.loads(raw)


def _read_json_from_path_or_stdin(args: List[str]) -> Dict[str, Any]:
    if len(args) >= 1 and args[0] == "--stdin":
        return _read_json_from_stdin()
    if len(args) >= 1:
        return _read_json_from_file(Path(args[0]))
    raise ValueError("JSONファイルパス or --stdin が必要です。")


def _cosine_sim(a: List[float], b: List[float]) -> float:
    va = np.asarray(a, dtype=float)
    vb = np.asarray(b, dtype=float)
    na = float(np.linalg.norm(va))
    nb = float(np.linalg.norm(vb))
    if na <= 1e-12 or nb <= 1e-12:
        return 0.0
    return float(np.dot(va, vb) / (na * nb))


def _clamp01(x: float) -> float:
    return max(0.0, min(1.0, x))


def _score_scalar(p: float, t: float, allow: float, eps: float = 1e-9) -> float:
    """
    allow: 許容差（相対でなく“絶対差”で扱う）
    """
    if allow <= eps:
        return 100.0 if abs(p - t) <= eps else 0.0
    d = abs(p - t) / allow
    return 100.0 * _clamp01(1.0 - d)


def _get(d: Dict[str, Any], key: str, default: float = 0.0) -> float:
    try:
        return float(d.get(key, default))
    except Exception:
        return float(default)


def _pc_vec12(features: Dict[str, Any]) -> List[float]:
    return [_get(features, f"pc_{i}", 0.0) for i in range(12)]


def _axis_scores(
    P: Dict[str, Any],
    T: Dict[str, Any],
    cfg: Dict[str, Any],
) -> Dict[str, float]:
    """
    P/T: featuresByName dict（FINAL_COLUMNSの要素が入ってる想定）
    cfg: difficultyや許容幅
    """
    # ---- 難易度スケール（小さいほど厳しい）----
    difficulty = str(cfg.get("difficulty", "normal")).lower()
    if difficulty == "easy":
        k = 1.25
    elif difficulty == "hard":
        k = 0.80
    else:
        k = 1.0

    # ---- 許容幅（ざっくりデフォルト。後で調整していく前提）----
    allow_dir = float(cfg.get("allow_dir", 0.25)) * k
    allow_ratio = float(cfg.get("allow_ratio", 0.25)) * k
    allow_range = float(cfg.get("allow_range", 8.0)) * k        # MIDI幅
    allow_std = float(cfg.get("allow_std", 6.0)) * k            # MIDI標準偏差
    allow_interval = float(cfg.get("allow_interval", 2.5)) * k  # 半音
    allow_dur = float(cfg.get("allow_dur", 0.50)) * k           # 四分音符単位の平均/分散
    allow_sync = float(cfg.get("allow_sync", 0.25)) * k
    allow_density = float(cfg.get("allow_density", 3.0)) * k    # per bar系

    # =========================
    # 1) 音の動き（メロディ構造）
    # =========================
    s_range = _score_scalar(_get(P, "pitch_range_midi"), _get(T, "pitch_range_midi"), allow_range)
    s_std = _score_scalar(_get(P, "pitch_std_midi"), _get(T, "pitch_std_midi"), allow_std)
    s_int_mean = _score_scalar(_get(P, "interval_abs_mean_semitone"), _get(T, "interval_abs_mean_semitone"), allow_interval)
    s_int_std = _score_scalar(_get(P, "interval_abs_std_semitone"), _get(T, "interval_abs_std_semitone"), allow_interval)
    s_step = _score_scalar(_get(P, "step_ratio"), _get(T, "step_ratio"), allow_ratio)
    s_leap = _score_scalar(_get(P, "leap_ratio"), _get(T, "leap_ratio"), allow_ratio)
    s_up = _score_scalar(_get(P, "direction_up_ratio"), _get(T, "direction_up_ratio"), allow_dir)
    s_chg = _score_scalar(_get(P, "direction_change_rate"), _get(T, "direction_change_rate"), allow_dir)

    move = (
        0.15 * s_range + 0.10 * s_std +
        0.15 * s_int_mean + 0.10 * s_int_std +
        0.15 * s_step + 0.15 * s_leap +
        0.10 * s_up + 0.10 * s_chg
    )

    # =========================
    # 2) 音の密度（配置）
    # =========================
    s_note = _score_scalar(_get(P, "note_count_per_bar"), _get(T, "note_count_per_bar"), allow_density)
    s_onset = _score_scalar(_get(P, "onset_count_per_bar"), _get(T, "onset_count_per_bar"), allow_density)
    s_rest = _score_scalar(_get(P, "rest_ratio_time"), _get(T, "rest_ratio_time"), allow_ratio)
    s_short = _score_scalar(_get(P, "short_note_ratio"), _get(T, "short_note_ratio"), allow_ratio)

    density = 0.35 * s_note + 0.35 * s_onset + 0.15 * s_rest + 0.15 * s_short

    # =========================
    # 3) 音の秩序（スケール・和声感）
    # =========================
    pcP = _pc_vec12(P)
    pcT = _pc_vec12(T)
    s_pc = 100.0 * _cosine_sim(pcP, pcT)

    s_keyfit = _score_scalar(_get(P, "key_fit_ratio"), _get(T, "key_fit_ratio"), allow_ratio)
    s_chroma = _score_scalar(_get(P, "chromatic_ratio"), _get(T, "chromatic_ratio"), allow_ratio)
    s_accid = _score_scalar(_get(P, "accidental_note_ratio"), _get(T, "accidental_note_ratio"), allow_ratio)
    s_tonic_end = _score_scalar(_get(P, "tonic_ending_flag"), _get(T, "tonic_ending_flag"), 1.0)

    order = 0.60 * s_pc + 0.15 * s_keyfit + 0.10 * s_chroma + 0.10 * s_accid + 0.05 * s_tonic_end

    # =========================
    # 4) 時間の安定性（リズム）
    # =========================
    s_dur_mean = _score_scalar(_get(P, "dur_mean_quarter"), _get(T, "dur_mean_quarter"), allow_dur)
    s_dur_std = _score_scalar(_get(P, "dur_std_quarter"), _get(T, "dur_std_quarter"), allow_dur)
    s_str = _score_scalar(_get(P, "strongbeat_hit_ratio"), _get(T, "strongbeat_hit_ratio"), allow_ratio)
    s_off = _score_scalar(_get(P, "offbeat_ratio"), _get(T, "offbeat_ratio"), allow_ratio)
    s_sync = _score_scalar(_get(P, "syncopation_score"), _get(T, "syncopation_score"), allow_sync)

    rhythm = 0.25 * s_dur_mean + 0.20 * s_dur_std + 0.20 * s_str + 0.15 * s_off + 0.20 * s_sync

    # 5) 感情は別で（モデル出力とテンプレemotionを比較）
    return {
        "move": float(move),
        "density": float(density),
        "order": float(order),
        "rhythm": float(rhythm),
    }


def _emotion_score(pred_vec: List[float], template_vec: List[float]) -> float:
    # 4次元ベクトル想定（ラベル数が違う場合は外で整形）
    return 100.0 * _cosine_sim(pred_vec, template_vec)


def main() -> int:
    try:
        cmd = _read_json_from_path_or_stdin(sys.argv[1:])

        meta_path = Path(cmd.get("metaPath", "")).expanduser()
        if str(meta_path).strip() == "":
            raise ValueError("metaPath が必要です。")

        meta = load_meta(meta_path)

        model_path = cmd.get("modelPath", "")
        if str(model_path).strip() == "":
            model_path = meta.get("model_path", "")
        model_path = Path(model_path).expanduser()

        # プレイヤー特徴
        p_feats = cmd.get("featuresByName")
        if not isinstance(p_feats, dict):
            raise ValueError("featuresByName (dict) が必要です。")

        # テンプレ特徴（同じ列名dict）
        t_feats = cmd.get("templateFeaturesByName")
        if not isinstance(t_feats, dict):
            raise ValueError("templateFeaturesByName (dict) が必要です。")

        # ===== 感情推論 =====
        pred = predict_with_saved_model(
            model_path=model_path,
            meta=meta,
            feat_obj={"featuresByName": p_feats},
        )

        # ===== 5軸採点（感情以外の4軸 + 後で感情を足す）=====
        score_cfg = cmd.get("scoreConfig", {})
        if not isinstance(score_cfg, dict):
            score_cfg = {}

        axis4 = _axis_scores(p_feats, t_feats, score_cfg)

        # ===== 感情スコア =====
        # テンプレ目標の感情ベクトル：templateEmotionVec を推奨
        template_vec = cmd.get("templateEmotionVec")
        if template_vec is None:
            # 代替：templateEmotionByClass {trust:0.7,...} があれば classes順に並べる
            teb = cmd.get("templateEmotionByClass")
            if isinstance(teb, dict):
                classes = pred["classes"]
                template_vec = [float(teb.get(str(c), 0.0)) for c in classes]

        if isinstance(template_vec, list) and len(template_vec) == len(pred["proba_vec"]):
            emotion = _emotion_score(pred["proba_vec"], [float(x) for x in template_vec])
        else:
            # テンプレが無い場合は「モデルの確信度」を感情スコア代替にする（暫定）
            emotion = 100.0 * float(max(pred["proba_vec"]))

        # ===== 合成（重みは後で調整）=====
        w = score_cfg.get("weights", {})
        if not isinstance(w, dict):
            w = {}
        w_move = float(w.get("move", 0.25))
        w_density = float(w.get("density", 0.15))
        w_order = float(w.get("order", 0.20))
        w_rhythm = float(w.get("rhythm", 0.25))
        w_emotion = float(w.get("emotion", 0.15))

        total = (
            w_move * axis4["move"]
            + w_density * axis4["density"]
            + w_order * axis4["order"]
            + w_rhythm * axis4["rhythm"]
            + w_emotion * emotion
        )

        out = {
            "ok": True,
            "emotion_pred": {
                "label": pred["label"],
                "proba_by_class": pred["proba_by_class"],
                "proba_vec": pred["proba_vec"],
                "classes": pred["classes"],
            },
            "scores": {
                **axis4,
                "emotion": float(emotion),
                "total": float(total),
            }
        }
        print(json.dumps(out, ensure_ascii=False))
        return 0

    except Exception as e:
        err = {"ok": False, "error": {"type": type(e).__name__, "message": str(e)}}
        print(json.dumps(err, ensure_ascii=False))
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
