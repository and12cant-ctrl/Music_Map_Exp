# -*- coding: utf-8 -*-
# edit_score_json.py
# 標準入力からJSONを受け取り、MusicXMLを編集して書き出す
import sys, json
from music21 import converter, note, stream

def main():
    data = json.loads(sys.stdin.read())

    in_xml  = data["inputXml"]
    out_xml = data["outputXml"]
    pitches = data.get("addPitches", ["C4","D4","E4","F4"])
    ql      = float(data.get("quarterLength", 1.0))  # 4分音符=1.0
    part_name = data.get("partName", "Melody")
    insert_at_measure = int(data.get("insertAtMeasure", 1))  # 今回は未使用（シンプルに先頭へ）

    score = converter.parse(in_xml)

    # 追加するパートを作成
    mel = stream.Part()
    mel.id = part_name
    for p in pitches:
        n = note.Note(p)
        n.quarterLength = ql
        mel.append(n)

    # シンプル：スコア先頭に挿入（必要に応じて小節位置指定に拡張可）
    score.insert(0, mel)

    score.write("musicxml", out_xml)
    print(json.dumps({
        "status": "ok",
        "input": in_xml,
        "output": out_xml,
        "notesAdded": len(pitches)
    }))

if __name__ == "__main__":
    main()