using NUnit.Framework;
using System.IO;
using UnityEngine;

public class Test : MonoBehaviour
{
//    軸 低   中 高
//動き なめらか    ほどよく動く 大きく跳ねる
//密度 まばら 適度 ぎっしり
//秩序 不安定 やや曖昧 安定している
//リズム 揺れる やや安定 安定している

    public static string MusicXML = Path.Combine(FluidSynthPaths.EmotionDir, "UnClass", "Chop-28-2_Intro", "Chop-28-2_INTRO_m1-4_ts4_4_E0.230_S0.875_MAIN.musicxml");
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    //List templateEmotionVec = new List<float> {
    //0.05f,  // trust
    //0.85f,  // sad
    //0.05f,  // fear
    //0.05f   // anxiety
    //};
void Start()
    {
        string AS = $"{FluidSynthPaths.micromambaExe} run -r " +
            $"{FluidSynthPaths.micromambaroot} -n unitypy ^\r\npython -u \"C:\\Music_Map_Exp\\ScriptsPy\\tag_templates_4axis.py\" ^\r\n\"C:\\Music_Map_Exp\\MusicData\\Emotion\\trust.csv\" ^\r\n\"C:\\Music_Map_Exp\\MusicData\\Emotion\\sad.csv\" ^\r\n\"C:\\Music_Map_Exp\\MusicData\\Emotion\\fear.csv\" ^\r\n\"C:\\Music_Map_Exp\\MusicData\\Emotion\\anxiety.csv\" ^\r\n--out_csv \"C:\\Music_Map_Exp\\MusicData\\Emotion\\tagged_templates.csv\" ^\r\n--out_json \"C:\\Music_Map_Exp\\MusicData\\Emotion\\thresholds_4axis.json\"";
        //MusicXMLStart.AnalyzeEmotionFromMusicXML(MusicXML,null,,"normal");
        PythonReturn.Run()

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
