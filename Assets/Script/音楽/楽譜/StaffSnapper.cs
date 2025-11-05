using UnityEngine;

public class StaffSnapper : MonoBehaviour
{
    public enum Clef { Treble,Bass}

    [Header("Layout")]
    public Clef clef = Clef.Treble;
    public float lineSpacing = 14f; // px: 線と線の距離
    public float baseLineY = 14;    // 最下線の中心Y（ローカル）
    public int visibleSteps = 17;   // 例: E4(0)〜G6(…): 線/スペース含む段数

    [Header("Snapping")]
    public float snapRadius = 10f;  //近づいたら吸着

    public RectTransform Rect => (RectTransform)transform;

    // 下端のstepIndexをどの音にするか（Treble最下線E4=0 という設計例）
    public int PitchToStepIndex(int midi)
    {
        // 例: Treble基準=E4(MIDI 64)をline/space=0として、
        // 1 diatonic ステップごとに±1進む（クロマチックじゃなくドレミ進行）
        // 実際には key signature / accidentals と分けて考える。
        int e4 = 64;

        // C, D, E, F, G, A, B のうち、半音差とステップ差を持つテーブル化が現実的。
        // 簡易: 12音を diatonic へ投影する近似（実務ではテーブル推奨）
        return MidiToDiatonicStepsFromE4(midi); // 下の補助関数で適当に定義
    }
    public float StepIndexToY(int stepIndex)
    {
        return baseLineY + stepIndex * (lineSpacing / 2f);
    }

    public int YToNearestStepIndex(float localY)
    {
        float f = (localY - baseLineY) / (lineSpacing / 2f);
        return Mathf.RoundToInt(f);
    }
    private int MidiToDiatonicStepsFromE4(int midi)
    {
        // ざっくり：E4=64 を 0 に、完全/長短音程をだいたいの比率で丸める簡易版
        // 正式には C,D,E,F,G,A,B の並びで半音数に応じて step を進めるマップを用意。
        int[] semitoneToStep = {
            /* 0 C  */ 0,
            /* 1 C# */ 0,
            /* 2 D  */ 1,
            /* 3 D# */ 1,
            /* 4 E  */ 2,
            /* 5 F  */ 3,
            /* 6 F# */ 3,
            /* 7 G  */ 4,
            /* 8 G# */ 4,
            /* 9 A  */ 5,
            /*10 A# */ 5,
            /*11 B  */ 6
        };
        int diff = midi - 64; // from E4
        int oct = diff / 12;
        int rem = (diff % 12 + 12) % 12;
        return oct * 7 + (semitoneToStep[rem] - semitoneToStep[4]); // Eを基準0へ
    }

}
