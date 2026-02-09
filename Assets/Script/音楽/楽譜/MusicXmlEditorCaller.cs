using Sirenix.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class EditCommand
{
    public string inputXml;
    public string outputXml;
    public string partName ;
    //public string instrument;
    public string voice;
    public Node[] nodes;
}

public class MusicXmlEditorCaller : MonoBehaviour
{
    [SerializeField] private Node_Place nodes;
    [SerializeField] private MidiPlayer midiPlayer;
    [SerializeField] private Xml_to_MIDI xml_to_MIDI;
    [SerializeField] private MusicXMLStart XMLStart;
    [SerializeField] private Button But;
    [SerializeField]private Moniter _moniter;
    private string basePath;
    public string Json = "";
    void Awake()
    {
        But.onClick.AddListener(() =>
        {
            Score_Xml_MIDI();
        });

    }
    [ContextMenu("XML => MIDI(micromamba)")]

    public void Score_Xml_MIDI() {
        #region MyRegion
        var sb = new StringBuilder();
        foreach (var node in nodes._nodes)
        {
            sb.Append(node._pitch.ToString());
        }
        string ans = sb.ToString();
        //UnityEngine.Debug.Log(ans);
        #endregion

        string pythonPath = Path.Combine(FluidSynthPaths.micromambaroot, "envs/unitypy/python.exe");
        string scriptPath = Path.Combine(FluidSynthPaths.scriptPy, "edit_score_json.py");
        string inputXml = Path.Combine(FluidSynthPaths.ScoresDir, "empty_score.xml");
        string outputXml = Path.Combine(FluidSynthPaths.ScoresDir, "edited_score_MAIN.xml");
        string partName = Part.A.ToString();
        string InCSV = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "Player_Extract.csv");
        string OutCSV = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "Player_Tag.csv");

        #region JSON文字列に変換し、標準出力へ
        var cmd = new EditCommand
        {
            inputXml = inputXml,
            outputXml = outputXml,
            partName = partName,
            nodes = nodes._nodes.ToArray()
        };
        string json = JsonUtility.ToJson(cmd);
        #endregion

        #region Pythonプロセス起動
        var psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
            UseShellExecute = false,
            RedirectStandardInput = true,   //Unity=>Python
            RedirectStandardOutput = true,  //Python=>Unity
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        #endregion
        try
        {
            #region JSONを送信し、出力を受け取る
            using var p = Process.Start(psi);
            using (var sw = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false)))
            {
                sw.Write(json);     //Unity=>Pythonへ送信
            }
            //using var p = Process.Start(psi);
            if (p == null)
            {
                UnityEngine.Debug.LogError("Process.Start が null を返しました。pythonPath / scriptPath を確認してください。");
                return;
            }

            string stdout = p.StandardOutput.ReadToEnd();     //Python=>Unity
            string stderr = p.StandardError.ReadToEnd();      //エラーキャッチ
            p.WaitForExit();
            #endregion
            UnityEngine.Debug.Log($"[Python STDOUT]\n{stdout}");
            if (!string.IsNullOrWhiteSpace(stderr))
                UnityEngine.Debug.LogWarning($"[Python STDERR]\n{stderr}");

        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError(ex);
        }

        XMLStart.AnalyzeMusicXML(outputXml);
        xml_to_MIDI.RunVeny();
        midiPlayer.MidiStart();
        Tagged_template.Extract_to_Tag(OutCSV, InCSV);
        XMLStart.Extract_to_Tag(OutCSV, InCSV);
        var result = GetValue(OutCSV);
        _moniter.EmoTextChenge(result.MaxM,result.MinO);
    }

    public (string MaxM, string MinO) GetValue(string csvPath)
    {
        var lines = File.ReadAllLines(csvPath);

        string lastLine = lines[lines.Length - 1];

        var cols = lastLine.Split(',');

        string valueM = cols[12]; 
        string valueO = cols[14];
        return (valueM, valueO);
    }
}