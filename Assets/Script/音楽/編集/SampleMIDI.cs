using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.DedicatedServer;
using UnityEngine.UI;
public class EmoJson
{
    public string MusicXML;
    public string Soundfont;

}

public class SampleMIDI : MonoBehaviour
{
    private string Sf2Path = FluidSynthPaths.SoundFontFile;
    private string pyScript = Path.Combine(FluidSynthPaths.scriptPy, "Midi_Cheker.py");
    private string testpy = Path.Combine(FluidSynthPaths.scriptPy, "TestScript.py");
    private string envName = "unitypy";
    private string XmlPath = "";
    private string XmlFolder = Path.Combine(FluidSynthPaths.dataroot,"Output");
    #region 感情
    private string sadDir = Path.Combine(FluidSynthPaths.EmotionDir, "sad");
    private string fearDir = Path.Combine(FluidSynthPaths.EmotionDir, "fear");
    private string trustDir = Path.Combine(FluidSynthPaths.EmotionDir, "trust");
    private string anxietyDir = Path.Combine(FluidSynthPaths.EmotionDir, "anxiety");
    private string unClassDir = Path.Combine(FluidSynthPaths.EmotionDir, "UnClass");
    #endregion
    #region ボタン
    [SerializeField] private Button btnSad;
    [SerializeField] private Button btnFear;
    [SerializeField] private Button btnTrust;
    [SerializeField] private Button btnAnxiety;
    [SerializeField] private Button btnUnclass;
    [SerializeField] private Button btnRun;
    #endregion

    private bool isPressed = false;
    private void Start()
    {
        #region ボタン設定
        btnSad.onClick.AddListener(() =>
        {
            JudgeXml(Path.Combine(FluidSynthPaths.EmotionDir, "sad"));
        });
        btnFear.onClick.AddListener(() =>
        {
            JudgeXml(Path.Combine(FluidSynthPaths.EmotionDir, "fear"));
        });
        btnTrust.onClick.AddListener(() =>
        {
            JudgeXml(Path.Combine(FluidSynthPaths.EmotionDir, "trust"));

        });
        btnAnxiety.onClick.AddListener(() =>
        {
            JudgeXml(Path.Combine(FluidSynthPaths.EmotionDir, "anxiety"));
        });
        btnUnclass.onClick.AddListener(() =>
        {
            JudgeXml(Path.Combine(FluidSynthPaths.EmotionDir, "UnClass"));
        });
        btnRun.onClick.AddListener(() =>
        {
            StartCoroutine(Run());
        });
        #endregion
    }

    IEnumerator Run()
    {
        string[] SubFolder = Directory.GetDirectories(XmlFolder);
        foreach (string folderPath in SubFolder)
        {
            string[] files = Directory.GetFiles(folderPath);
            foreach (string file in files)
            {
                XmlPath = Path.Combine(folderPath,file);
                UnityEngine.Debug.Log(XmlPath);
                //PlayXml(XmlPath, Sf2Path);
                TestMamba();

                yield return new WaitUntil(() => isPressed);
                isPressed = false;
            }
        }
    }
    void PlayXml(string xml,string sf2)
    {
        var cmd = new EmoJson
        {
            MusicXML = xml,
            Soundfont = sf2
        };
        string json = JsonUtility.ToJson(cmd);

        string tmpJsonPath = Path.Combine(Path.GetTempPath(), $"unity_cmd_{Guid.NewGuid():N}.json");
        //var args  = $"run -r \"{FluidSynthPaths.micromambaroot}\" -n {envName}"+
        //            $"python -u \"{pyScript}\" --root \"{json}\"";
        var args =
            $"run -r \"{FluidSynthPaths.micromambaroot}\" -n {envName} " +
            $"python -u \"{testpy}\"";
        var psi = new ProcessStartInfo
        {
            FileName = FluidSynthPaths.micromambaExe,
            Arguments = args,

            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,

            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        //File.WriteAllText(tmpJsonPath, json);

        StringBuilder stdoutBuf = new StringBuilder();
        StringBuilder stderrBuf = new StringBuilder();
        Process process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) return;
            stdoutBuf.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (s, e) =>
        {
            if (e.Data != null) return;
            stderrBuf.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        process.WaitForExit();
        if (stdoutBuf.Length > 0)UnityEngine. Debug.Log("[PY STDOUT]\n" + stdoutBuf);
        if (stderrBuf.Length > 0) UnityEngine.Debug.LogError("[PY STDERR]\n" + stderrBuf);
        //process.Dispose();
    }

    void TestMamba()
    {

        var psi = new ProcessStartInfo
        {
            FileName = FluidSynthPaths.micromambaExe,
            Arguments =
            $"run -r \"{FluidSynthPaths.micromambaroot}\" -n {envName} " +
            $"python -u \"{testpy}\"",

            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,

            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
          
        StringBuilder stdoutBuf = new StringBuilder();
        Process _p = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _p.OutputDataReceived += (s, e) =>
        {
            if (e.Data != null) stdoutBuf.AppendLine(e.Data);
        };
        _p.Start();
        _p.BeginOutputReadLine();
        _p.WaitForExit();   // ← 終了を待つ

        if (stdoutBuf.Length > 0)
        {
            UnityEngine.Debug.Log("[PY STDOUT] " + stdoutBuf.ToString());
        }
    }

    void JudgeXml(string FolderName)
    {
         UnityEngine.Debug.Log($"XML = {XmlPath}");
        UnityEngine.Debug.Log($"Folder = {FolderName}");
        File.Move(XmlPath, FolderName);
        isPressed = true;
    }
}
