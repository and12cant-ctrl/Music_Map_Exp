import sys
import json
from pathlib import Path
from music21 import stream 

def eprint (*args,**kwargs):
    "エラー出力"
    print(*args,file=sys.stderr,**kwargs)

def read_json_from_stdin():
    "標準出力で送られたjsonを読む"
    data = sys.stdin.read()
    if not data.strip():
        eprint("[edit_score_json] 標準入力(JSON)が空です。Unity 側の送信を確認してください。")
        sys.exit(1)

    try:
        cmd =json.loads(data)
    except json.JSONDecodeError as e:
        eprint(f"[edit_score_json] JSON デコードに失敗しました: {e}")
        sys.exit(1)
    return cmd

from collections import defaultdict

def get_or_create_measure(target_part,bar_number: int,measure_cache):
    #    bar_number 番の小節 Measure を取得 or 新規作成して返す。
    #    再利用のため measure_cache[bar_number] に保存する。

    m = measure_cache.get(bar_number)
    if m is not None:
        return m
    try:
        # 指定小節だけを取り出す
        m_stream = target_part.measures(bar_number, bar_number)
        if m_stream is None or len(m_stream) == 0:
            # その小節がまだ存在しない → 新規作成
            m = stream.Measure(number=bar_number)
            target_part.append(m)
        else:
            # 既存小節を流用
            m = m_stream[0]
    except Exception:
        # measures() が失敗した場合の保険：新規作成
        m = stream.Measure(number=bar_number)
        target_part.append(m)

    measure_cache[bar_number] = m
    return m 

def get_bar_length_quarter(target_part, default: float = 4.0) -> float:
    """
    拍子記号 (TimeSignature) から 1 小節の長さを quarterLength で取得。
    例: 4/4 → 4.0, 3/4 → 3.0。
    拍子が見つからない場合は default (4.0) を返す。
    """
    # class 名での検索なので import は不要
    ts = target_part.recurse().getElementsByClass("TimeSignature").first()
    if ts is not None:
        return ts.barDuration.quarterLength
    return default

def insert_note_across_measures(
    target_part,
    measure_cache,
    start_bar_number: int,
    start_offset: float,
    duration: float,
    pitch: str,
):
    """
    単音 Note 用。
    start_bar_number 小節目の start_offset から duration 分の長さの音を入れる。
    duration が小節長を超える場合は、小節をまたぐように分割して配置する。
    """
    from music21 import note  # ローカル import

    bar_len = get_bar_length_quarter(target_part)  # 例: 4.0
    remaining = duration
    current_bar = max(1, int(start_bar_number))
    offset_in_bar = max(0.0, float(start_offset))

    while remaining > 0:
        m = get_or_create_measure(target_part, current_bar, measure_cache)

        space_in_bar = bar_len - offset_in_bar
        if space_in_bar <= 0:
            # この小節はもう埋まっている → 次の小節へ
            current_bar += 1
            offset_in_bar = 0.0
            continue

        this_dur = min(remaining, space_in_bar)

        try:
            n = note.Note(pitch)
        except Exception as e:
            eprint(f"[edit_score_json] pitch='{pitch}' の Note 生成に失敗。スキップします。\n{e}")
            return

        n.quarterLength = this_dur
        m.insert(offset_in_bar, n)

        remaining -= this_dur
        current_bar += 1
        offset_in_bar = 0.0

def insert_chord_across_measures(
    target_part,
    measure_cache,
    start_bar_number: int,
    start_offset: float,
    duration: float,
    pitches,
):
    """
    和音 Chord 用。
    pitches は ["C4", "E4", "G4"] のような pitch 文字列リストを想定。
    duration が小節長を超える場合は、小節をまたぐように分割して配置する。
    """
    from music21 import chord  # ローカル import

    if not pitches:
        return

    bar_len = get_bar_length_quarter(target_part)
    remaining = duration
    current_bar = max(1, int(start_bar_number))
    offset_in_bar = max(0.0, float(start_offset))

    while remaining > 0:
        m = get_or_create_measure(target_part, current_bar, measure_cache)

        space_in_bar = bar_len - offset_in_bar
        if space_in_bar <= 0:
            current_bar += 1
            offset_in_bar = 0.0
            continue

        this_dur = min(remaining, space_in_bar)

        try:
            c = chord.Chord(pitches)
        except Exception as e:
            eprint(f"[edit_score_json] pitches='{pitches}' の Chord 生成に失敗。スキップします。\n{e}")
            return

        c.quarterLength = this_dur
        m.insert(offset_in_bar, c)

        remaining -= this_dur
        current_bar += 1
        offset_in_bar = 0.0


def main():
    "Json受け取り"
    cmd = read_json_from_stdin()
    try:
        input_xml = Path(cmd["inputXml"])
        output_xml = Path(cmd["outputXml"])
    except KeyError as e:
        eprint(f"[edit_score_json] 必須フィールドが不足しています: {e}")
        sys.exit(1)

    part_name = cmd.get("partName", "Melody")
    nodes     = cmd.get("nodes",[])
    instrument_name = cmd.get("instrument",None)
    # パスを正規化
    input_xml = input_xml.expanduser().resolve()
    output_xml = output_xml.expanduser().resolve()

    if not input_xml.exists():
        eprint(f"[edit_score_json] 入力 XML が見つかりません: {input_xml}")
        sys.exit(1)

    # 親フォルダが無ければ作成
    output_xml.parent.mkdir(parents=True, exist_ok=True)

    # 2) music21 で XML 読み込み
    try:
        from music21 import converter, note, stream,instrument
    except ImportError as e:
        eprint(f"[edit_score_json] music21 のインポートに失敗しました。環境を確認してください。\n{e}")
        sys.exit(1)

    try:
        score = converter.parse(str(input_xml))
    except Exception as e:
        eprint(f"[edit_score_json] XML の読み込みに失敗しました: {input_xml.name}\n{e}")
        sys.exit(1)

    # 3) パートを探す（partName 一致、なければ先頭パート）

    try:
        parts = list(score.parts)
    except Exception:
        # parts 属性がない場合は score 自体をパートとして扱う
        parts = [score]

    target_part = None
    for p in parts:
        # part.partName が None の場合もあるので安全に取り出す
        p_name = getattr(p, "partName", None)
        if p_name == part_name:
            target_part = p
            break

    if target_part is None:
        new_part = stream.Part()
        new_part.id=str(part_name)
        new_part.partName = str(part_name)

        if instrument_name is not None:
            try:
                inst_class = getattr(instrument,instrument_name)
                new_part.insert(0,inst_class())
            except AttributeError:
                # 楽器名が music21 に存在しない場合のエラー
                eprint(f"[edit_score_json] 楽器 '{instrument_name}' は music21 に存在しません。Piano をセットします。")
        else:
            new_part.insert(0, instrument.Piano())
        new_part.insert(0, instrument.Piano())
        score.insert(len(parts),new_part)
        target_part = new_part
        eprint(f"[edit_score_json] partName='{part_name}' が見つからなかったため、新規パートを作成しました。")

    # --- 小節キャッシュ（bar_number → Measure オブジェクト） ---
    from collections import defaultdict
    measure_cache = defaultdict(lambda: None)
    # --- ノード情報から Note を生成してパートに追加 ---
    for ninfo in nodes:
        try:
            pitch       =ninfo["_pitch"]
            duration    =float(ninfo["_duration"])
            start_bar   =float(ninfo["_startBar"])
            bar_number  =float(ninfo["_bar"])
            harmony     =bool(ninfo["_harmony"])
        except KeyError as e:
            eprint(f"[edit_score_json] Nodeの必須フィールドが不足しています: {e}")
            continue

        pitches = ninfo.get("_pitches", None)
        # 小節番号は 1 始まりで想定
        if bar_number < 1:
            bar_number = 1
        bar_number_int = int(bar_number)
        # 小節を取得 or 作成
        if pitches is not None:
            # pitches は ["C4","E4","G4"] などを期待
            insert_chord_across_measures(
                target_part=target_part,
                measure_cache=measure_cache,
                start_bar_number=bar_number_int,
                start_offset=start_bar,
                duration=duration,
                pitches=pitches,
            )
        else:
            # 単音 Note として扱う
            insert_note_across_measures(
                target_part=target_part,
                measure_cache=measure_cache,
                start_bar_number=bar_number_int,
                start_offset=start_bar,
                duration=duration,
                pitch=pitch,
            )

    # 5) 出力 XML として保存
    try:
        score.write("musicXml", fp=str(output_xml))
    except Exception as e:
        eprint(f"[edit_score_json] MusicXML の書き出しに失敗しました: {output_xml}\n{e}")
        sys.exit(1)

    # 6) 標準出力に結果情報を出す（Unity 側のログで確認用）
    print("[edit_score_json] OK")
    print(f"[edit_score_json] input : {input_xml}")
    print(f"[edit_score_json] output: {output_xml}")
    print(f"[edit_score_json] part  : {part_name}")
    print(f"[edit_score_json] nodes:{len(nodes)}")

    sys.exit(0)


if __name__ == "__main__":
    main()