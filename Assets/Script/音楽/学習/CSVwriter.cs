
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;


public class CSVwriter : MonoBehaviour
{
    string path = Path.Combine(FluidSynthPaths.CSVDir,"features.csv");
    string scriptsPy = Path.Combine(FluidSynthPaths.scriptPy, "extract_features.py");

    int row;
    int col = 3;

    private void Update()
    {
        string script =
        $"run -r \"{FluidSynthPaths.micromambaroot}\" " +
        $"-n unitypy " +
        $"python -u \"{scriptsPy}\" " +
        $"\"{mainMusicXmlPath}\"";
    
        string json = PyRunner.RunAndGetStdout(FluidSynthPaths.micromambaExe,scriptsPy);

        string csvPath = Path.Combine(Application.persistentDataPath, "features.csv");

        WriteCsvHeaderIfNotExists(csvPath);
        WriterCSV(csvPath, json);
    }
    void WriterCSV(string Path,string jsonText)
    {
        var dict = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonText);

        var cells = new List<string>(FINAL_COLUMNS.Length);
        foreach (var key in FINAL_COLUMNS)
        {
            if (dict.TryGetValue(key, out var value) && value != null)
            {
                cells.Add(value.ToString());
            }
            else
            {
                // 欠損値は空セル（または 0 / NaN にしてもよい）
                cells.Add("");
            }
        }
        string line = string.Join(",", cells);
        File.AppendAllText(
            Path, line +"\n",
            new UTF8Encoding(false )
            );
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

    static readonly string[] FINAL_COLUMNS =
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
    "strongbeat_hit_ratio","offbeat_ratio"
};
}
