using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.DedicatedServer;
using UnityEngine.UI;

[System.Serializable]
public class ExtractResponse
{
    public bool ok;
    public string error;
    public int savedCount;
    public string[] outputFiles;
}

[System.Serializable]
public class ExtractCommand
{
    public string midiPath; 
    public string InputRoot;
    public string outputPath; 
    public string outputDir;
    public int minMeasures;
    public int maxMeasures;
    public int maxPhrasesPerSong;
    public int maxSongs;
    public bool others;
}


public class SelectBarXml: MonoBehaviour
{
    [SerializeField] private Button btnOne;
    public void Start()
    {
        btnOne.onClick.AddListener(() =>
        {
            RunExtract();
        });
    }
    public void RunExtract()
    {
        string projectRoot = FluidSynthPaths.ProjectRoot;
        string micromambaExe = FluidSynthPaths.micromambaExe;
        string scriptPath = Path.Combine(FluidSynthPaths.scriptPy, "extract_lmd_phrases.py");

        string inputRoot = Path.Combine(FluidSynthPaths.MusicDataroot, "LMD_clean");
        string outputDir = Path.Combine(FluidSynthPaths.MusicDataroot, "Output");

        string[] MusicerName = Directory.GetDirectories(inputRoot);
        string arguments =
            $"run -r \"{FluidSynthPaths.micromambaroot}\" -n unitypy python \"{scriptPath}\"";
       
        foreach (string FolderName in MusicerName) {
            string[] ForderName = Directory.GetDirectories(FolderName);

            foreach (string midiPath in Directory.GetFiles(FolderName, "*.mid")) {
                var cmdObj = new ExtractCommand
                {
                    midiPath = midiPath,
                    outputDir = outputDir,
                    minMeasures = 2,
                    maxMeasures = 4,
                    maxPhrasesPerSong = 10,
                    maxSongs = 100,
                    others = true,
                };

                string json = JsonUtility.ToJson(cmdObj);

                var r = PythonReturn.RunWithStdin(
                    exePath: micromambaExe,
                    arguments: arguments,
                    stdinText: json,
                    timeoutMs: 120_000,                 // 必要なら伸ばす
                    encoding: Encoding.UTF8
                );

                #region JSONパース
                PythonReturn.PyLog.LogStdout(r.Stdout, contextTag: "PY", contextInfo: midiPath);
                PythonReturn.PyLog.LogStderr(r.Stderr, contextTag: "PY", contextInfo: midiPath);

                if (r.TimedOut)
                {
                    UnityEngine.Debug.LogError($"Python timed out. midi={midiPath}");
                    continue;
                }

                if (r.ExitCode != 0)
                {
                    UnityEngine.Debug.LogError($"Python exit={r.ExitCode}. midi={midiPath}\n{r.Stderr}");
                    continue;
                }
                string stdout = (r.Stdout ?? "").Trim();
                try
                {
                    var resp = JsonUtility.FromJson<ExtractResponse>(stdout);

                    if (resp != null && resp.ok)
                    {
                        if (resp.outputFiles != null)
                        {
                            foreach (var f in resp.outputFiles)
                                UnityEngine.Debug.Log($"output:{f}");
                        }
                        UnityEngine.Debug.Log($"savedCount={resp.savedCount}");
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Python error: {resp?.error}\nraw:\n{stdout}");
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"JSON parse error: {e}\nraw:\n{stdout}");
                }
                #endregion
            } 
        }
    }
}
