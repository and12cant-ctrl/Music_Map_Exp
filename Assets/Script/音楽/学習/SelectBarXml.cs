using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
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
        //string midiPath = Path.Combine(inputRoot, "Bittersweet_Symphony.mid");
        //string midiPath = "";
        string outputDir = Path.Combine(FluidSynthPaths.MusicDataroot, "Output");

        foreach (string midiPath in Directory.GetFiles(inputRoot,"*.mid")) {
            var cmdObj = new ExtractCommand
            {
                midiPath = midiPath,
                outputDir = outputDir,
                minMeasures = 2,
                maxMeasures = 4,
                maxPhrasesPerSong = 10,
                maxSongs = 200
            };

            string json = JsonUtility.ToJson(cmdObj);
            var psi = new ProcessStartInfo
            {
                FileName = micromambaExe,
                Arguments = $"run -r \"{FluidSynthPaths.micromambaroot}\" -n unitypy python \"{scriptPath}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using (var proc = new Process())
            {
                proc.StartInfo = psi;
                proc.Start();

                proc.StandardInput.WriteLine(json);
                proc.StandardInput.Close();

                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();

                proc.WaitForExit();

                UnityEngine.Debug.Log($"[Py STDOUT]\n{stdout}");
                if (!string.IsNullOrEmpty(stderr))
                {
                    UnityEngine.Debug.LogWarning($"[Py STDERR]\n{stderr}");
                }

                try
                {
                    var resp = JsonUtility.FromJson<ExtractResponse>(stdout);

                    if (resp != null && resp.ok)
                    {
                        foreach (var f in resp.outputFiles)
                        {
                            UnityEngine.Debug.Log($"output:{f}");
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Python error: {resp?.error}");
                    }
                }
                catch (System.Exception e)
                {
                    UnityEngine.Debug.LogError($"JSON parse error: {e}\nraw: {stdout}");
                }
            }
        }
    }
}
