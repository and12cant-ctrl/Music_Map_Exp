
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Animations;


#region 判定用クラス
[Serializable]
public class HintScores
{
    public float move;
    public float density;
    public float order;
    public float rhythm;
    public float emotion;
    public float total;
}

[Serializable]
public class EmotionPred
{
    public string label;
    public Dictionary<string, float> proba_by_class;
    public List<float> proba_vec;
    public List<string> classes;
}

[Serializable]


public class JudgeRequest
{
    public string metaPath;
    public string modelPath;

    // プレイヤー特徴（extract_features出力から作る）
    public Dictionary<string, float> featuresByName;

    // テンプレ特徴（同じ列名で持つ。まだ無ければ null でもOKにしておく）
    public Dictionary<string, float> templateFeaturesByName;

    // テンプレ感情（あれば）
    public List<float> templateEmotionVec;

    public Dictionary<string, object> scoreConfig;
}
#endregion

public class MusicXMLStart : MonoBehaviour
{
    string extractpy = Path.Combine(FluidSynthPaths.scriptPy, "extract_features.py");
    string mambaRoot = Path.Combine(FluidSynthPaths.micromambaroot);
    string mambaExe = Path.Combine(FluidSynthPaths.micromambaExe);
    string CSV = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "Player_Extract.csv");
    string emo_lgbm = Path.Combine(FluidSynthPaths.GBMModelDir, "emotion_lgbm.txt");
    string emo_Json = Path.Combine(FluidSynthPaths.GBMModelDir, "emotion_lgbm_meta.json");

    string mamba = FluidSynthPaths.micromambaExe;
    string scPy = Path.Combine(FluidSynthPaths.scriptPy, "tag_templates_4axis.py");
    #region MyRegion
    string trsut = Path.Combine(FluidSynthPaths.CSVDir, "trust.csv");
    string sad = Path.Combine(FluidSynthPaths.CSVDir, "sad.csv");
    string fear = Path.Combine(FluidSynthPaths.CSVDir, "fear.csv");
    string anxiety = Path.Combine(FluidSynthPaths.CSVDir, "anxiety.csv");
    string AllEmo = Path.Combine(FluidSynthPaths.CSVDir, "all_emotions.csv");
    string Player = Path.Combine(FluidSynthPaths.CSVDir, "Hint","Player_tag.csv");
    #endregion

    string tagged = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "tagged_templates.csv");
    string axis4 = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "thresholds_4axis.json");
    private string PlayRes = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "player_result.json");

    public void Start()
    {
        #region 一回きり
        //Debug.Log("koko");
        //string[] emotionCsvs =
        //{
        //    Path.Combine(FluidSynthPaths.CSVDir, "trust.csv"),
        //    Path.Combine(FluidSynthPaths.CSVDir, "sad.csv"),
        //    Path.Combine(FluidSynthPaths.CSVDir, "fear.csv"),
        //    Path.Combine(FluidSynthPaths.CSVDir, "anxiety.csv")
        //};

        //MergeCsvFiles(emotionCsvs, AllEmo);
        //Extract_to_Tag(tagged,AllEmo);
        #endregion

    }
    public void AnalyzeMusicXML(string musicXmlPath)
    {
        // -----------------------------
        // MusicXML → featuresRow
        // -----------------------------

        string extractArgs =
            $"run -r \"{mambaRoot}\" -n unitypy " +
            $"python -u \"{extractpy}\" \"{musicXmlPath}\" --out_csv " +
            $"\"{CSV}\" --root_dir {FluidSynthPaths.ProjectRoot} "+
            $"--mode overwrite";

        var featResult = PythonReturn.Run(
            exePath: mambaExe,
            arguments: extractArgs,
            timeoutMs: 60_000,
            encoding: Encoding.UTF8
        );

        Debug.Log("[PY extract STDOUT]\n" + featResult.Stdout);
        Debug.Log("[PY extract STDERR]\n" + featResult.Stderr);

        if (featResult.ExitCode != 0)
            throw new Exception("extract_features.py 実行失敗");

        string featJsonText = ExtractLastJsonObject(featResult.Stdout);

        var playerFeaturesByName = BuildFeaturesByNameFromFlatJson(
            featJsonText,
            CSVwriter.FINAL_COLUMNS.ToList(),
            fillna: 0f
        );

    }

    private static string ExtractLastJsonObject(string s)
    {
        if (string.IsNullOrEmpty(s))
            throw new Exception("stdout が空です");

        int start = s.LastIndexOf("\"ok\"");
        if (start < 0)
            if (start < 0) start = s.LastIndexOf('{');
        int depth = 0;
        for (int i = start; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    // start..i が1つのJSONオブジェクト
                    return s.Substring(start, i - start + 1).Trim();
                }
            }
        }

        throw new Exception("JSONの '}' が閉じません:\n" + s.Substring(start));
    }

    private static Dictionary<string, float> BuildFeaturesByNameFromFlatJson(
        string flatJson,
        List<string> finalColumns,
        float fillna = 0f
    )
    {
        var jo = JObject.Parse(flatJson);
        var dict = new Dictionary<string, float>(finalColumns.Count);

        foreach (var col in finalColumns)
        {
            JToken tok = jo[col];
            if (tok == null || tok.Type == JTokenType.Null)
            {
                dict[col] = fillna;
                continue;
            }

            if (tok.Type == JTokenType.Integer || tok.Type == JTokenType.Float)
            {
                dict[col] = tok.Value<float>();
            }
            else
            {
                dict[col] = fillna;
            }
        }
        return dict;
    }

    public void Extract_to_Tag(string outCSV, string InCSV)
    {
        //public static string MusicXML = Path.Combine(FluidSynthPaths.EmotionDir, "UnClass", "Chop-28-2_Intro", "Chop-28-2_INTRO_m1-4_ts4_4_E0.230_S0.875_MAIN.musicxml");


        string AS = $"run -r {FluidSynthPaths.micromambaroot} -n unitypy " +
            $"python -u \"{scPy}\" \"{InCSV}\" " +
            $"--out_csv \"{outCSV}\" " +
            $"--out_json \"{axis4}\" " +
            $"--emo_model \"{emo_lgbm}\" " +
            $"--emo_meta \"{emo_Json}\" ";
        //MusicXMLStart.AnalyzeEmotionFromMusicXML(MusicXML,null,,"normal");
        var featResult = PythonReturn.Run(
            exePath: mamba,
            arguments: AS,
            timeoutMs: 60_000,
            encoding: Encoding.UTF8
        );
    }

    public void Extract_to_Tag_Ply(string outCSV, string InCSV)
    {
        string AS = $"run -r {FluidSynthPaths.micromambaroot} -n unitypy " +
            $"python -u \"{scPy}\" \"{InCSV}\" " +
            $"--out_csv \"{outCSV}\" " +
            $"--out_json \"{axis4}\" " +
            $"--emo_model \"{emo_lgbm}\" " +
            $"--emo_meta \"{emo_Json}\" " +
            $"--tag_meta \"{axis4}\" ";
        //MusicXMLStart.AnalyzeEmotionFromMusicXML(MusicXML,null,,"normal");
        var featResult = PythonReturn.Run(
            exePath: mamba,
            arguments: AS,
            timeoutMs: 60_000,
            encoding: Encoding.UTF8
        );
    }

    public void MergeCsvFiles(string[] inputCsvs, string outputCsv)
    {
        if (inputCsvs == null || inputCsvs.Length == 0)
        {
            Debug.LogError("CSV list is empty");
            return;
        }

        bool headerWritten = false;

        using (var writer = new StreamWriter(outputCsv, false)) // false = overwrite
        {
            foreach (var csv in inputCsvs)
            {
                if (!File.Exists(csv))
                {
                    Debug.LogWarning($"CSV not found: {csv}");
                    continue;
                }

                var lines = File.ReadAllLines(csv);

                if (lines.Length == 0)
                    continue;

                // 最初だけヘッダを書く
                if (!headerWritten)
                {
                    writer.WriteLine(lines[0]);
                    headerWritten = true;
                }

                //データ行だけ追加（ヘッダ除外）
                for (int i = 1; i < lines.Length; i++)
                {
                    if (!string.IsNullOrWhiteSpace(lines[i]))
                        writer.WriteLine(lines[i]);
                }
            }
        }

        Debug.Log($"CSV merged → {outputCsv}");
    }
    
}
