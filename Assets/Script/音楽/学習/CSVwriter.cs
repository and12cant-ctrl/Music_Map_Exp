using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;


public class CSVwriter : MonoBehaviour
{
    string CsvPy = Path.Combine(FluidSynthPaths.scriptPy, "extract_features.py");
    string trainPy = Path.Combine(FluidSynthPaths.scriptPy, "train_emotion_model.py");
    string MusicXmlPath = Path.Combine(FluidSynthPaths.EmotionDir);
    string ModelFile = Path.Combine(FluidSynthPaths.ModelDir, "GBM_Model");


    [SerializeField] Button Example;

    private void Start()
    {
        Example.onClick.AddListener(() =>
        {
            //ChengeCSV(Path.Combine(MusicXmlPath));
            trainToCSV(Path.Combine(FluidSynthPaths.CSVDir));
        });
    }
    void ChengeCSV(string EmoFolderName)
    {
        foreach (string EmoFolder in Directory.GetDirectories(EmoFolderName))
        {
            string emo = Path.GetFileName(EmoFolder).ToLowerInvariant(); 
            string outCsv = Path.Combine(FluidSynthPaths.CSVDir, $"{emo}.csv");
            bool first = true; 

            foreach (string XMLFile in Directory.GetDirectories(EmoFolder)){
                foreach (string EmoXmls in Directory.EnumerateFiles(XMLFile, "*.musicxml"))
                {
                    Debug.Log("Start!!");
                    var stemUpper = Path.GetFileNameWithoutExtension(EmoXmls);
                    string su = stemUpper.ToUpperInvariant();
                    if (su.Contains("FULL")) continue;
                    if (!su.Contains("MAIN")) continue;
                    string mode = first ? "overwrite" : "append";
                    first = false;

                    string arguments =
                    $"run -r \"{FluidSynthPaths.micromambaroot}\" " +
                    $"-n unitypy " +
                    $"python -u \"{CsvPy}\" " +
                    $"\"{EmoXmls}\" --out_csv \"{outCsv}\" " +
                    $"--root_dir \"{FluidSynthPaths.EmotionDir}\" "+
                    $"--mode {mode}";

                    var r = PythonReturn.RunWithStdin(
                        exePath: FluidSynthPaths.micromambaExe,
                        arguments: arguments,
                        stdinText: null,
                        timeoutMs: 120_000,
                        encoding: Encoding.UTF8
                    );

                    //string csvPath = Path.Combine(Application.persistentDataPath, "features.csv");
                    PythonReturn.PyLog.LogStderr(r.Stderr, contextTag: "PY", contextInfo: EmoXmls);

                } 
            }
        }
    }

    void trainToCSV(string CSVDirectory)
    {
        Debug.Log($"Start!!{CSVDirectory}");
        var csvByLabel = new Dictionary<string, string>();

        foreach (string CSVEmo in Directory.GetFiles(CSVDirectory,"*.csv")){
            string label = Path.GetFileNameWithoutExtension(CSVEmo);
            csvByLabel[label] = CSVEmo.Replace("\\", "/");
        }
    

        var root = new Dictionary<string, object>
        {
            ["csvByLabel"] = csvByLabel,
            ["outputDir"] = ModelFile,
            ["modelName"] = "emotion_lgbm",
            ["testSize"] = 0.2,
            ["seed"] = 42,
            ["fillna"] = 0
        };

        string json = JsonConvert.SerializeObject(root,Formatting.Indented);
        Debug.Log("[JSON TO PY]\n" + json);
        string arguments =
                $"run -r \"{FluidSynthPaths.micromambaroot}\" " +
                $"-n unitypy " +
                $"python -u \"{trainPy}\" --stdin";

        var r = PythonReturn.RunWithStdin(
            exePath: FluidSynthPaths.micromambaExe,
            arguments: arguments,
            stdinText: json,
            timeoutMs: 120_000,
            encoding: Encoding.UTF8
        );

        //string csvPath = Path.Combine(Application.persistentDataPath, "features.csv");
        PythonReturn.PyLog.LogStdout(r.Stdout, contextTag: "PY");
        PythonReturn.PyLog.LogStderr(r.Stderr, contextTag: "PY");
    }


    static void WriteCsvHeaderIfNotExists(string csvPath)
    {
        if (File.Exists(csvPath)) return;

        string header = string.Join(",", FINAL_COLUMNS);
        File.WriteAllText(csvPath, header + "\n", new UTF8Encoding(false));
    }

    static string[] LoadColumns(string columnsJsonPath)
    {
        var json = File.ReadAllText(columnsJsonPath, Encoding.UTF8);
        return JsonConvert.DeserializeObject<string[]>(json);
    }

   public static readonly string[] FINAL_COLUMNS =
{
    // basics
    "time_signature_num","time_signature_den","tempo_bpm_est","bars_len","total_duration_quarter","pickup_flag",

    // MAIN
    "pitch_std_midi","pitch_range_midi",
    "pc_0","pc_1","pc_2","pc_3","pc_4","pc_5","pc_6","pc_7","pc_8","pc_9","pc_10","pc_11",
    "interval_abs_mean_semitone","interval_abs_std_semitone",
    "step_ratio","leap_ratio","direction_up_ratio","direction_change_rate",
    "note_count_per_bar","onset_count_per_bar",
    "dur_mean_quarter","dur_std_quarter","short_note_ratio","rest_ratio_time","syncopation_score",
    "key_fit_ratio","chromatic_ratio","accidental_note_ratio","tonic_ending_flag",

    // ACC
    "bass_mean_midi","bass_range_midi","bass_step_ratio","bass_leap_ratio",
    "chord_event_count_per_bar","mean_chord_size","chord_size_std",
    "polyphony_ratio_time","accomp_density_time",
    "dissonance_ratio","tritone_ratio","minor_third_ratio","major_third_ratio",
    "seventh_chord_ratio","sus_ratio",
    "key_fit_ratio_acc",
    "pc_acc_0","pc_acc_1","pc_acc_2","pc_acc_3","pc_acc_4","pc_acc_5",
    "pc_acc_6","pc_acc_7","pc_acc_8","pc_acc_9","pc_acc_10","pc_acc_11",
    "chromatic_ratio_acc","cadence_strength_score",
    "strongbeat_hit_ratio","offbeat_ratio",   "strongbeat_hit_ratio","offbeat_ratio"
};
}
