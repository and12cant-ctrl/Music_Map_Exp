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

        var psi = new ProcessStartInfo
        {
            FileName = pythonPath,
            Arguments = $"\"{scriptPath}\"",
            WorkingDirectory = Path.GetDirectoryName(scriptPath),
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };

        try
        {
            using var p = Process.Start(psi);
            using (var sw = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false)))
            {
                sw.Write(json);
            }

            string stdout = p.StandardOutput.ReadToEnd();
            string stderr = p.StandardError.ReadToEnd();
            p.WaitForExit();

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
