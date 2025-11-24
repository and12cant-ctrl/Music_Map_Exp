import sys
import json
from pathlib import Path

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
        except KeyError as e:
            eprint(f"[edit_score_json] Nodeの必須フィールドが不足しています: {e}")
            continue
        # 小節番号は 1 始まりで想定
        if bar_number < 1:
            bar_number = 1

        # 小節を取得 or 作成
        if measure_cache[bar_number] is None:
            try:
                m_stream == target_part.measures(bar_number,bar_number)
                if m_stream is None or len(m-stream) == 0:
                    m = stream.Measure(number=bar_number)
                    target_part.append(m)
                else:
                    m = m_stream[0]
            except Exception:
                m = stream.Measure(number = bar_number)
                target_part.append(m)
            measure_cache[bar_number]  = m
        else:
            m = measure_cache[bar_number]

        # Note作成
        try:
            n = note.Note(pitch)
        except Exception as e:
            eprint(f"[edit_score_json] pitch='{pitch}' のNote生成に失敗。スキップします。\n{e}")
            continue

        n.quarterLength= duration

        #小節内のstart_barに配置
        offset = max(0.0,start_bar)
        m.insert(offset,n)

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