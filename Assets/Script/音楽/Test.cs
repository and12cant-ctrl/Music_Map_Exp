using NUnit.Framework;
using System.IO;
using System.Text;
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
        string mamba = FluidSynthPaths.micromambaExe;
        string scPy =Path.Combine(FluidSynthPaths.scriptPy, "tag_templates_4axis.py");
        string trsut = Path.Combine(FluidSynthPaths.CSVDir, "trust.csv");
        string sad = Path.Combine(FluidSynthPaths.CSVDir, "sad.csv");
        string fear = Path.Combine(FluidSynthPaths.CSVDir, "fear.csv");
        string anxiety = Path.Combine(FluidSynthPaths.CSVDir, "anxiety.csv");
        string tagged = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "tagged_templates.csv");
        string Json = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "thresholds_4axis.json");
        string AS = $"run -r {FluidSynthPaths.micromambaroot} -n unitypy " +
            $"python -u \"{scPy}\" \"{trsut}\" \"{sad}\" \"{fear}\" \"{anxiety}\" " +
            $"--out_csv \"{tagged}\" " +
            $"--out_json \"{Json}\"";
        //MusicXMLStart.AnalyzeEmotionFromMusicXML(MusicXML,null,,"normal");
        var featResult = PythonReturn.Run(
            exePath: mamba,
            arguments: AS,
            timeoutMs: 60_000,
            encoding: Encoding.UTF8
        );
        Debug.Log("[PY extract STDOUT]\n" + featResult.Stdout);
        Debug.Log("[PY extract STDERR]\n" + featResult.Stderr);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
