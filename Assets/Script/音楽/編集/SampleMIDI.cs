
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

using UnityEngine.UI;
public class EmoJson
{
    public string MusicXML;
    public string Soundfont;
    public string fluidsynth;
}

public class SampleMIDI : MonoBehaviour
{
    private string Sf2Path = FluidSynthPaths.SoundFontFile;
    private string pyScript = Path.Combine(FluidSynthPaths.scriptPy, "Midi_Cheker.py");
    private string envName = "unitypy";
    private string XmlPath = "";
    private string XmlFolder = Path.Combine(FluidSynthPaths.dataroot,"Output");
    private string fluidExe = FluidSynthPaths.FluidSynthExe;
    private string XMLName = "";
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
            UnityEngine.Debug.Log(Path.Combine(FluidSynthPaths.EmotionDir, "sad"));
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

                XMLName = Path.GetFileName(file);
                XmlPath = Path.Combine(folderPath,file);
                UnityEngine.Debug.Log(XmlPath);
                PlayXml(XmlPath, Sf2Path);
                //TestMamba();

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
            Soundfont = sf2,
            fluidsynth = fluidExe
        };
        string json = JsonUtility.ToJson(cmd);
        var args = $"run -r \"{FluidSynthPaths.micromambaroot}\" -n {envName} " +
                   $"python -X utf8 -u \"{pyScript}\"";
        var psi = new ProcessStartInfo
        {
            FileName = FluidSynthPaths.micromambaExe,
            Arguments = args,

            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,

            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        StringBuilder stdoutBuf = new StringBuilder();
        StringBuilder stderrBuf = new StringBuilder();
        Process _proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

        _proc.OutputDataReceived += (s, e) =>
        {
            if (e.Data == null) return;
            stdoutBuf.AppendLine(e.Data);
        };
        _proc.ErrorDataReceived += (s, e) =>
        {
            if (e.Data == null) return;
            stderrBuf.AppendLine(e.Data);
        };
        UnityEngine. Debug.Log("start");
        _proc.Start();
        _proc.BeginOutputReadLine();
        _proc.BeginErrorReadLine();
        _proc.StandardInput.Write(json);
        _proc.StandardInput.Close(); 

        if (stdoutBuf.Length > 0)UnityEngine. Debug.Log("[PY STDOUT]" + stdoutBuf.ToString());
        if (stderrBuf.Length > 0) UnityEngine.Debug.LogError("[PY STDERR]\n" + stderrBuf.ToString());
        
        //_proc.Dispose();
    }

    void JudgeXml(string FolderName)
    {
        string folder = Path.Combine(FolderName, XMLName);
        UnityEngine.Debug.Log($"XML = {XMLName}");
        UnityEngine.Debug.Log($"Folder = {FolderName}");
        UnityEngine.Debug.Log($"Folder = {folder}");
        if (XmlPath == "") return;
        //File.Move(XmlPath, folder);
        if (File.Exists(folder))
        {
            UnityEngine. Debug.Log("見つかりました: " + folder);
        }
        else
        {
            UnityEngine.Debug.Log("見つかりません");
        }
        isPressed = true;
    }
}
