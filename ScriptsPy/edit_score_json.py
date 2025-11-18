import sys
import json
from pathlib import Path

def eprint (*args,**kwargs):
    "エラー出力"
    print(*args,flie=sys.stderr,**kwargs)

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
    cmd = read_json_from_stdin

    try:
        input_xml = Path(cmd["inputxml"])
        output_xml = Path(cmd["outputXml"])
    except KeyError as e:
        eprint(f"[edit_score_json] 必須フィールドが不足しています: {e}")
        sys.exit(1)

    add_pitches = cmd.get("addPitches",[])
    quarter_length = float(cmd.get("quarterLength", 1.0))
    part_name = cmd.get("partName", "Melody")
    insert_at_mesure = int(cmd.get("insertAtMesure", 1))

    input_xml = input_xml.expanduser().resolve()
    output_xml = output_xml.expanduser().resolve()

    if not input_xml.exists():
        eprint(f"[edit_score_json] 入力 XML が見つかりません: {input_xml}")
        sys.exit(1)

    # 親フォルダが無ければ作成
    output_xml.parent.mkdir(parents=True, exist_ok=True)

    # 2) music21 で XML 読み込み
    try:
        from music21 import converter, note, stream
    except ImportError as e:
        eprint(f"[edit_score_json] music21 のインポートに失敗しました。環境を確認してください。\n{e}")
        sys.exit(1)

    try:
        score = converter.parse(str(input_xml))
    except Exception as e:
        eprint(f"[edit_score_json] XML の読み込みに失敗しました: {input_xml.name}\n{e}")
        sys.exit(1)

    # 3) パートを探す（partName 一致、なければ先頭パート）
    target_part = None
    try:
        parts = score.parts
    except Exception:
        # parts 属性がない場合は score 自体をパートとして扱う
        parts = [score]

    for p in parts:
        # part.partName が None の場合もあるので安全に取り出す
        p_name = getattr(p, "partName", None)
        if p_name == part_name:
            target_part = p
            break

    if target_part is None:
        # 見つからなかった場合は先頭パートを使用
        target_part = parts[0] if len(parts) > 0 else score
        eprint(f"[edit_score_json] partName='{part_name}' が見つからなかったため、先頭パートを使用します。")

    # 4) 指定小節を取得して音符を追加
    # insertAtMesure は 1 始まりと想定
    measure_number = max(1, insert_at_mesure)

    # measures(n, n) でその小節だけを取り出す
    try:
        target_measure_stream = target_part.measures(measure_number, measure_number)
    except Exception:
        target_measure_stream = None

    if target_measure_stream is None or len(target_measure_stream) == 0:
        # 小節が存在しない場合は新しい小節を作る
        m = stream.Measure(number=measure_number)
        target_part.append(m)
        target_measure = m
    else:
        # measures() は Stream を返すので、先頭要素を小節として扱う
        target_measure = target_measure_stream[0]

    # その小節の末尾から音符を追加していく
    current_offset = target_measure.highestTime  # 既にある音符の後ろから開始
    for pitch_name in add_pitches:
        try:
            n = note.Note(pitch_name)
        except Exception as e:
            eprint(f"[edit_score_json] pitch '{pitch_name}' の Note 生成に失敗しました。スキップします。\n{e}")
            continue

        n.quarterLength = quarter_length
        # 小節内の current_offset に配置
        target_measure.insert(current_offset, n)
        current_offset += quarter_length

    # 5) 出力 XML として保存
    try:
        score.write("musicxml", fp=str(output_xml))
    except Exception as e:
        eprint(f"[edit_score_json] MusicXML の書き出しに失敗しました: {output_xml}\n{e}")
        sys.exit(1)

    # 6) 標準出力に結果情報を出す（Unity 側のログで確認用）
    print("[edit_score_json] OK")
    print(f"[edit_score_json] input : {input_xml}")
    print(f"[edit_score_json] output: {output_xml}")
    print(f"[edit_score_json] part  : {part_name}")
    print(f"[edit_score_json] meas  : {measure_number}")
    print(f"[edit_score_json] notes : {add_pitches}")

    sys.exit(0)


if __name__ == "__main__":
    main()