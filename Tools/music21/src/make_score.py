from pathlib import Path
from music21 import stream,meter,tempo,key,note

def make_empty_score(with_empty_bar=True):
    sc = stream.Score(id='EmptyScore')
    p = stream.Part(id='Part1')

    p.insert(0, meter.TimeSignature('4/4'))
    p.insert(0, tempo.MetronomeMark(number=60))
    p.insert(0, key.Key('C'))

    if with_empty_bar:
        m1 = stream.Measure(number=1)
        m1.append(note.Rest(quarterLength=4.0))  # 4/4 の全休符
        p.append(m1)

    sc.append(p)
    return sc

if __name__ == '__main__':
    # このスクリプトの場所から Unity プロジェクトの StreamingAssets/scores を求める
    this = Path(__file__).resolve()
    unity_root = this.parents[3]                     # <ProjectRoot>
    out_dir = unity_root / 'StreamingAssets' / 'scores'
    out_dir.mkdir(parents=True, exist_ok=True)

    sc = make_empty_score(with_empty_bar=True)
    out_path = out_dir / 'empty_score.xml'
    sc.write('musicxml', fp=str(out_path))
    print(f'Wrote: {out_path}')