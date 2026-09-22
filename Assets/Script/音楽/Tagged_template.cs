
using System.IO;


public static class Tagged_template
{
//    軸 低   中 高
//動き なめらか    ほどよく動く 大きく跳ねる
//密度 まばら 適度 ぎっしり
//秩序 不安定 やや曖昧 安定している
//リズム 揺れる やや安定 安定している
    public static void Extract_to_Tag(string outCSV,string InCSV)
    {

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

        var daemon = MidiDaemonClient.Instance;
        if (daemon == null || !daemon.IsRunning)
            throw new System.InvalidOperationException("MidiDaemonClient is not running.");

        var featResult = daemon.RunPythonScript(
            scPy,
            new[]
            {
                InCSV,
                "--out_csv", outCSV,
                "--out_json", Json,
                "--band_mode", "rank",
                "--emo_model", modelPath,
                "--emo_meta", metaPath,
            },
            timeoutMs: 60_000);

        if (featResult.TimedOut || featResult.ExitCode != 0)
            throw new System.Exception($"tag_templates_4axis.py failed. exit={featResult.ExitCode}\n{featResult.Stderr}");

    }

}
