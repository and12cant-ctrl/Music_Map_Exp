using System.IO;
using UnityEngine;

public static class FluidSynthPaths
{
    // プロジェクトルート … Assets の1つ上
    public static string ProjectRoot =>
        Directory.GetParent(Application.dataPath)!.FullName;

    public static string ToolsDir => Path.Combine(ProjectRoot, "Tools");
    public static string FluidSynthDir => Path.Combine(ToolsDir, "fluidsynth");
    public static string FluidSynthExe => Path.Combine(FluidSynthDir, "fluidsynth.exe");

    public static string StreamingAssetsDir => Path.Combine(Application.dataPath, "StreamingAssets");
    public static string ScoresDir => Path.Combine(StreamingAssetsDir, "Scores");
    public static string SoundFontsDir => Path.Combine(StreamingAssetsDir, "SoundFonts");
    public static string CacheDir => Path.Combine(StreamingAssetsDir, "Cache");

    public static string ToAbsoluteFromStreamingAssets(string relativeUnderSA)
        => Path.GetFullPath(Path.Combine(StreamingAssetsDir, relativeUnderSA.Replace('/', Path.DirectorySeparatorChar)));
}