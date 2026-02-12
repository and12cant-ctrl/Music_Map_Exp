
using System.IO;
using System.Text;


public static class Tagged_template
{
//    軸 低   中 高
//動き なめらか    ほどよく動く 大きく跳ねる
//密度 まばら 適度 ぎっしり
//秩序 不安定 やや曖昧 安定している
//リズム 揺れる やや安定 安定している
    public static void Extract_to_Tag(string outCSV,string InCSV)
    {

    string mamba = FluidSynthPaths.micromambaExe;
    string scPy = Path.Combine(FluidSynthPaths.scriptPy, "tag_templates_4axis.py");
    #region MyRegion
    string trsut = Path.Combine(FluidSynthPaths.CSVDir, "trust.csv");
    string sad = Path.Combine(FluidSynthPaths.CSVDir, "sad.csv");
    string fear = Path.Combine(FluidSynthPaths.CSVDir, "fear.csv");
    string anxiety = Path.Combine(FluidSynthPaths.CSVDir, "anxiety.csv");
    string Player = Path.Combine(FluidSynthPaths.CSVDir, "Player_tag.csv");
    #endregion

    string tagged = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "tagged_templates.csv");
    string Json = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "thresholds_4axis.json");
    string modelPath = Path.Combine(FluidSynthPaths.GBMModelDir, "emotion_lgbm.txt");
    string metaPath = Path.Combine(FluidSynthPaths.GBMModelDir, "emotion_lgbm_meta.json");

        string AS = $"run -r {FluidSynthPaths.micromambaroot} -n unitypy " +
            $"python -u \"{scPy}\" \"{InCSV}\" " +
            $"--out_csv \"{outCSV}\" " +
            $"--out_json \"{Json}\" " +
            $"--band_mode rank " +
            $"--emo_model \"{modelPath}\" " +
            $"--emo_meta \"{metaPath}\" ";
    //MusicXMLStart.AnalyzeEmotionFromMusicXML(MusicXML,null,,"normal");
    var featResult = PythonReturn.Run(
        exePath: mamba,
        arguments: AS,
        timeoutMs: 60_000,
        encoding: Encoding.UTF8
    );

    }

}
