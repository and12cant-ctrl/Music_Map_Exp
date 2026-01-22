using Melanchall.DryWetMidi.Composing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public class EmotionResult
{
    public string pred;
    public Dictionary<string, float> proba;
}

[Serializable]
class PredictRequest
{
    public string metaPath;
    public string modelPath;
    public List<float> featuresRow;
}

[Serializable]
class FeatureRowWrapper
{
    public List<float> featuresRow;
}

[Serializable]
class PredictResultWrapper
{
    public bool ok;
    public EmotionResult result;
}
public class MusicXMLStart : MonoBehaviour
{
    public static EmotionResult AnalyzeEmotionFromMusicXML(
    string musicXmlPath
    )
    {
        string extractpy = Path.Combine(FluidSynthPaths.scriptPy,"extract_features.py");
        string trainPy = Path.Combine(FluidSynthPaths.scriptPy, "train_emotion_model.py");
        string mambaRoot = Path.Combine(FluidSynthPaths.micromambaroot);
        string mambaExe = Path.Combine(FluidSynthPaths.micromambaExe);
        string emo_lgbm = Path.Combine(FluidSynthPaths.GBMModelDir, "emotion_lgbm.txt");
        string emo_Json = Path.Combine(FluidSynthPaths.GBMModelDir, "emotion_lgbm_meta.json");
        // -----------------------------
        // MusicXML Å® featuresRow
        // -----------------------------

        Debug.Log("musicXmlPath = " + musicXmlPath);
        Debug.Log("File.Exists = " + File.Exists(musicXmlPath));
        string extractArgs =
            $"run -r \"{mambaRoot}\" -n unitypy " +
            $"python -u \"{extractpy}\" \"{musicXmlPath}\" --out_json -";

        var featResult = PythonReturn.Run(
            exePath: mambaExe,
            arguments: extractArgs,
            timeoutMs: 60_000,
            encoding: Encoding.UTF8
        );

        Debug.Log("[PY extract STDOUT]\n" + featResult.Stdout);
        Debug.Log("[PY extract STDERR]\n" + featResult.Stderr);

        //if (!featResult.Stdout.Contains("featuresRow"))
        //    throw new Exception("ì¡í•ó íäèoÇ…é∏îsÇµÇ‹ÇµÇΩ");
        if (featResult.ExitCode != 0)
            throw new Exception("extract_features.py é¿çsé∏îs");

        string stdout = featResult.Stdout.Trim();
        int jsonStart = stdout.LastIndexOf('{');
        if (jsonStart < 0)
            throw new Exception("JSONÇ™å©Ç¬Ç©ÇËÇ‹ÇπÇÒ");

        string json = stdout.Substring(jsonStart);

        var featJson = JsonUtility.FromJson<FeatureRowWrapper>(featResult.Stdout);
        List<float> featuresRow = featJson.featuresRow;

        // -----------------------------
        // featuresRow Å® ä¥èÓêÑò_
        PredictRequest req = new PredictRequest
        {
            modelPath = emo_lgbm,
            metaPath = emo_Json,
            featuresRow = featuresRow
        };

        string predictJson = JsonUtility.ToJson(req);

        string predictArgs =
            $"run -r \"{mambaRoot}\" -n unitypy " +
            $"python -u \"{trainPy}\" --predict --stdin";

        var predResult = PythonReturn.RunWithStdin(
            exePath: mambaExe,
            arguments: predictArgs,
            stdinText: predictJson,
            timeoutMs: 30_000
        );

        if (!predResult.Stdout.Contains("\"ok\": true"))
            throw new Exception("ä¥èÓêÑò_Ç…é∏îsÇµÇ‹ÇµÇΩ\n" + predResult.Stdout);
        var wrapper = JsonUtility.FromJson<PredictResultWrapper>(predResult.Stdout);
        return wrapper.result;
    }

}
