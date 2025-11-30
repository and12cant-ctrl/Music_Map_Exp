using Sirenix.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

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
    private string basePath;
    void Awake()
    {
        //Run();
    }
    [ContextMenu("XML => MIDI(micromamba)")]

    public void Run() {
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
        string outputXml = Path.Combine(FluidSynthPaths.ScoresDir, "edited_score.xml");
        string partName = Part.A.ToString();
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
        //UnityEngine.Debug.Log(json);

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

            //if (File.Exists(outputXml))UnityEngine.Debug.Log($"出力OK: {outputXml}");
            //elseUnityEngine.Debug.LogError("出力ファイルが見つかりません。パス/権限を確認してください。");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError(ex);
        }
    }
}