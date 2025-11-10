using System.IO;
using UnityEngine;

public static class FluidSynthPaths
{
    // プロジェクトルート … Assets の1つ上
    //ターン開始時、鐘の音を鳴らす。ドクターWhoのtoyMakerのスパイスガールズの冒頭みたく
    public static string ProjectRoot =>
        Directory.GetParent(Application.dataPath)!.FullName;

    public static string ToolsDir => Path.Combine(ProjectRoot, "Tools");
    public static string scriptPy => Path.Combine(ProjectRoot, "ScriptsPy");
    public static string FluidSynthDir => Path.Combine(ToolsDir, "fluidsynth");
    public static string FluidSynthExe => Path.Combine(FluidSynthDir, "fluidsynth.exe");

    public static string StreamingAssetsDir => Path.Combine(Application.dataPath, "StreamingAssets");
    public static string ScoresDir => Path.Combine(StreamingAssetsDir, "scores");
    public static string SoundFontsDir => Path.Combine(FluidSynthDir, "SoundFonts");
    public static string SoundFontFile => Path.Combine(SoundFontsDir,"Arachno.sf2");
    public static string CacheDir => Path.Combine(StreamingAssetsDir, "Cache");
    public static string micromambaDir => Path.Combine(ToolsDir, "micromamba");
    public static string micromambaExe => Path.Combine(micromambaDir, "micromamba.exe");
    public static string micromambaroot => Path.Combine(micromambaDir, "mamba_root");

    public static string ToAbsoluteFromStreamingAssets(string relativeUnderSA)
        => Path.GetFullPath(Path.Combine(StreamingAssetsDir, relativeUnderSA.Replace('/', Path.DirectorySeparatorChar)));
}