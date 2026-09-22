
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

using UnityEngine.UI;
public class EmoSortMIDI : MonoBehaviour
{
    private string pyScript = Path.Combine(FluidSynthPaths.scriptPy, "Xml_to_Midi.py");
    private string XmlPath = "";
    private string XmlFolder = Path.Combine(FluidSynthPaths.MusicDataroot,"Output");
    private string XMLName = "";
    private string UnkownDir = Path.Combine(FluidSynthPaths.MusicDataroot, "temporary");
    private string KeyWord = "_FULL";
    private string ScondryName = "";
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
    [SerializeField] private Button btnExRun;
    #endregion

    private bool isPressed = false;
    private void Start()
    {
        string ExXML = Path.Combine(XmlFolder,
            "bach-invention-01",
            "bach-invention-01_m16-19_ts4_4_E0.570_S0.643_001.musicxml");
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
            StartCoroutine(Run(XmlFolder));
        });
        btnExRun.onClick.AddListener(() =>
        {
            //PlayXml(Tagged_template.MusicXML, Sf2Path);
        });
        #endregion
    }

    IEnumerator Run(string Dir)
    {
        foreach (var file in Directory.GetFiles(UnkownDir))
        {
            File.Delete(file);
        }
        string[] MusicFolders = Directory.GetDirectories(Dir);
        foreach (string TRYfolderPath in MusicFolders)
        {
            ScondryName = Path.GetFileName(TRYfolderPath);
            UnityEngine.Debug.Log($"{ScondryName}");
            string[] Intros = Directory.GetDirectories(TRYfolderPath);
            foreach (string str in Intros)
            {
                if (Directory.GetFiles(str).Length > 0)
                {
                    var Fullfile = Directory.GetFiles(str).
                    FirstOrDefault(f => Path.GetFileName(f).Contains(KeyWord));

                    XMLName = str;
                    XmlPath = Path.Combine(TRYfolderPath, Fullfile);
                    PlayXml(XmlPath);

                    yield return new WaitUntil(() => isPressed);
                    isPressed = false;
                }
            }
        }
    }


    void PlayXml(string xml)
    {
        string outputMidi = Path.Combine(UnkownDir, Path.GetFileNameWithoutExtension(xml) + ".mid");
        var daemon = MidiDaemonClient.Instance;
        if (daemon == null || !daemon.IsRunning)
        {
            UnityEngine.Debug.LogError("MidiDaemonClient is not running.");
            return;
        }

        var result = daemon.RunPythonScript(
            pyScript,
            new[] { xml, outputMidi },
            timeoutMs: 120_000);

        if (result.TimedOut || result.ExitCode != 0)
            UnityEngine.Debug.LogError($"XML to MIDI failed. exit={result.ExitCode}");
        if (!string.IsNullOrWhiteSpace(result.Stdout)) UnityEngine.Debug.Log("[PY STDOUT]" + result.Stdout);
        if (!string.IsNullOrWhiteSpace(result.Stderr)) UnityEngine.Debug.LogError("[PY STDERR]\n" + result.Stderr);
    }

    void JudgeXml(string FolderName)
    {
        string folder = Path.Combine(FolderName,$"{ScondryName}_{Path.GetFileName(XMLName)}");
        if (XmlPath == "") return;
        UnityEngine.Debug.Log($"元の場所{XMLName}\n移す場所{folder}");
        Directory.Move(XMLName, folder);
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

    void AllXMLJudge(string XmlDir)
    {
       
    }
}
