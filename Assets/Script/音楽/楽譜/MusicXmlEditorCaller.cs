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
    public string[] addPitches;
    public float quarterLength = 1;//四分音符
    public string partName = "Melody";
    public int insertAtMesure = 1; //拡張用
}
public class MusicXmlEditorCaller : MonoBehaviour
{
    [Header("Paths")]
    [Tooltip("仮想環境のpython.exeへのフルパス")]
    public string pythonPath = @"G:\Unity\Product\Music_Map_Exp\Tools_music21\.venc\Scripts\python.exe";
    [Tooltip("edit_score_json.pyのフルパス")]
    public string scriptPath = @"G:Unity\Product\Music_Map_Exp\Tools\music21\edit_dcore_json.py";

    [Header("Files")]
    public string inputXml = @"G:\Unity\Product\Music_Map_Exp\Scores\empty_score.xml";
    public string outputXml = @"G:\Unity\Product\Music_Map_Exp\Scores\edited_score.xml";

    [Header("Melody")]
    public string[] addPitches = new[] { "C4", "D4", "E4", "F4" };
    public float quarterLength = 1;
    public string partName = "Melody";
    public int insertAtMesure = 1;

    [ContextMenu("Run Edit (write edited_score.xml")]
    public void RunEdit()
    {
        #region JSON文字列に変換し、標準出力へ
        var cmd = new EditCommand   
        {
            inputXml = inputXml,
            outputXml = outputXml,
            addPitches = addPitches,
            quarterLength = quarterLength,
            partName = partName,
            insertAtMesure = insertAtMesure
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

            string stdout = p.StandardOutput.ReadToEnd();     //Python=>Unity
            string stderr = p.StandardError.ReadToEnd();      //エラーキャッチ
            p.WaitForExit();
            #endregion
            UnityEngine.Debug.Log($"[Python STDOUT]\n{stdout}");
            if (!string.IsNullOrWhiteSpace(stderr))
                UnityEngine.Debug.LogWarning($"[Python STDERR]\n{stderr}");

            if (File.Exists(outputXml))
                UnityEngine.Debug.Log($"出力OK: {outputXml}");
            else
                UnityEngine.Debug.LogError("出力ファイルが見つかりません。パス/権限を確認してください。");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError(ex);
        }
    }

}
