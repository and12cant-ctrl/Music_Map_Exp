using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
public class EmoJson
{
    private string MusicXML;
    private string Soundfont;

}

public class SampleMIDI : MonoBehaviour
{
    private string Sf2Path = FluidSynthPaths.SoundFontFile;
    private string pyCode = Path.Combine(FluidSynthPaths.scriptPy, "Midi_Cheker.py");

    private string XmlPath = "";
    private string XmlFolder = Path.Combine(FluidSynthPaths.dataroot,"Output");
    #region Š´î
    private string sadDir = Path.Combine(FluidSynthPaths.EmotionDir, "sad");
    private string fearDir = Path.Combine(FluidSynthPaths.EmotionDir, "fear");
    private string trustDir = Path.Combine(FluidSynthPaths.EmotionDir, "trust");
    private string anxietyDir = Path.Combine(FluidSynthPaths.EmotionDir, "anxiety");
    private string unClassDir = Path.Combine(FluidSynthPaths.EmotionDir, "UnClass");
    #endregion
    #region ƒ{ƒ^ƒ“
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
        //Run();
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
            StartCoroutine (Run());
        });
    }

    IEnumerator Run()
    {
        string[] SubFolder = Directory.GetDirectories(XmlFolder);
        foreach (string folderPath in SubFolder)
        {
            string[] files = Directory.GetFiles(folderPath);
            foreach (string file in files)
            {
                UnityEngine.Debug.Log(file);
                XmlPath = Path.GetFileName(file);
                UnityEngine.Debug.Log(XmlPath);
                PlayXml(XmlPath, Sf2Path);
                yield return new WaitUntil(() => isPressed);
                isPressed = false;
            }
        }
    }
    void PlayXml(string xml,string sf2)
    {
        string json = JsonUtility.ToJson(new
        {
            MusicXML = xml.Replace("\\", "/"),
            soundfont = sf2.Replace("\\", "/")
        });
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = FluidSynthPaths.micromambaExe,
            Arguments = $"{pyCode} \"{json}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        };
        Process.Start(psi);
    }

    void JudgeXml(string FileName)
    {
        File.Move(XmlPath, FileName);
        isPressed = true;
    }
}
