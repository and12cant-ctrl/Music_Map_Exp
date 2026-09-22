using System;
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
    public string voice;
    public NodeMapView[] nodes;
}

public class MusicXmlEditorCaller : MonoBehaviour
{
    [SerializeField] private Node_Place nodes;
    [SerializeField] private MidiDaemonClient midiDaemon;
    [SerializeField] private Xml_to_MIDI xml_to_MIDI;
    [SerializeField] private MusicXMLStart XMLStart;
    [SerializeField] private Button Judge;
    [SerializeField] private Button Arrangement;
    [SerializeField] private Moniter _moniter;
    [SerializeField] private HintSelectionManager HintManager;
    [SerializeField] private Music21ScoreImporter ScoreImporter;
    [SerializeField] private RectTransform NodesParent;
    [SerializeField] private SimpleStaffRenderer StafeRend;
    [SerializeField] public AnswerSet Answer;
    [SerializeField] private bool IsTest =true;
    private string basePath;
    public string Json => "";
    void Awake()
    {
        if (IsTest)
        {
            Judge.onClick.AddListener(() =>
            {
                Score_Xml_MIDI();
            });
            Arrangement.onClick.AddListener(() =>
            {
                ClearAllNode();
                string A = TaggedTemplatesCsvMatcher.FindAllMatches(HintManager.state);
                UnityEngine.Debug.Log(A);
                if (A != "")
                {
                    ScoreImporter.musicXmlFilePath = Path.Combine(FluidSynthPaths.EmotionDir, A);
                    ScoreImporter.ImportWithMusic21();
                }
            });
        };

    }

    public void Score_Xml_MIDI() {
        #region MyRegion
        var sb = new StringBuilder();
        foreach (var node in nodes._nodes)
        {
            sb.Append(node._pitch.ToString());
        }
        string ans = sb.ToString();
        #endregion

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

        if (midiDaemon == null)
        {
            UnityEngine.Debug.LogError("[MusicXML] MidiDaemonClientが設定されていません。");
            return;
        }

        var editResult = midiDaemon.RunPythonScript(
            scriptPath,
            stdinText: json,
            timeoutMs: 120_000);

        if (editResult.TimedOut || editResult.ExitCode != 0)
        {
            UnityEngine.Debug.LogError($"[MusicXML] edit_score_json.py failed. exit={editResult.ExitCode}\n{editResult.Stderr}");
            return;
        }

        UnityEngine.Debug.Log($"[Python STDOUT]\n{editResult.Stdout}");
        if (!string.IsNullOrWhiteSpace(editResult.Stderr))
            UnityEngine.Debug.LogWarning($"[Python STDERR]\n{editResult.Stderr}");

        XMLStart.AnalyzeMusicXML(outputXml);
        xml_to_MIDI.RunVeny();
        if (!PlayGeneratedMidi())
        {
            return;
        }

        //midiPlayer.MidiStart();
        XMLStart.Extract_to_Tag_Ply(OutCSV, InCSV);

        var result = GetValue(OutCSV);
 
        int LV4 =0;
        int EMO5= 0;

        TaggedTemplatesCsvMatcher.returnCSV( Answer ,OutCSV,out LV4,out EMO5);
        //_moniter.EmoTextChenge(result.MaxM, result.MinO,LV4,EMO5);
    }

    public (string MaxM, string MinO)GetValue(string csvPath)
    {
        var lines = File.ReadAllLines(csvPath);

        string lastLine = lines[lines.Length - 1];

        var cols = lastLine.Split(',');

        string valueM = cols[13]; 
        string valueO = cols[15];
        return (valueM, valueO);
    }

    public void ClearAllNode()
    {
        foreach (RectTransform t in StafeRend.partRoots.Values)
        {
            if (t == null) continue;

            for (int i = t.childCount - 1; i >= 0; i--)
            {
                Destroy(t.GetChild(i).gameObject);
            }
        }

        nodes._nodes.Clear();
    }
    public string Find_Xml()
    {
        string a = "";
        return a;
    }

    private bool PlayGeneratedMidi()
    {
        if (midiDaemon == null)
        {
            UnityEngine.Debug.LogError(
                "[MusicXML] MidiDaemonClientが設定されていません。"
            );
            return false;
        }

        if (!midiDaemon.IsInitialized)
        {
            string detail = string.IsNullOrEmpty(midiDaemon.InitializationError)
                ? "FluidSynthの初期化完了を待っています。"
                : midiDaemon.InitializationError;
            UnityEngine.Debug.LogError(
                $"[MusicXML] MIDIを再生できません: {detail}"
            );
            return false;
        }

        string midiPath = Path.GetFullPath(
            xml_to_MIDI.outputMid
        );

        if (!File.Exists(midiPath))
        {
            UnityEngine.Debug.LogError(
                $"[MusicXML] 生成されたMIDIが見つかりません: {midiPath}"
            );
            return false;
        }

        midiDaemon.PlayMid(
            midiPath,
            "editor_preview"
        );

        UnityEngine.Debug.Log(
            $"[MusicXML] 常駐デーモンへ再生を要求しました: {midiPath}"
        );

        return true;
    }
}
