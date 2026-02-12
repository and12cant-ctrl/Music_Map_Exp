using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine.InputSystem;

public static class TaggedTemplatesCsvMatcher
{
    public static readonly Dictionary<string, HintKey> HeaderMap =
    new(StringComparer.OrdinalIgnoreCase)
    {
            { "motion_band",  HintKey.Move },
            { "density_band", HintKey.Density },
            { "order_band",   HintKey.Order },
            { "rhythm_band",  HintKey.Rhythm },
            { "emo_max_label",HintKey.MaxEmo },
            { "emo_min_label",HintKey.MinEmo },
    };

    private static readonly HintKey[] RequiredKeys =
    {
        HintKey.Move, HintKey.Density, HintKey.Order, HintKey.Rhythm, HintKey.MaxEmo, HintKey.MinEmo
    };


    public static string FindAllMatches(HintSelectionState state)
    {
        int level4Count = 0;
        int emo5Count = 0;

        string csvPath = Path.Combine(FluidSynthPaths.CSVDir, "Hint", "tagged_templates.csv");

        var lines = File.ReadAllLines(csvPath);

        if (lines.Length == 0) return "";


        var header = SplitCsvLine(lines[0]);
        var indexByKey = ResolveIndexes(header);

        foreach (var key in RequiredKeys)
        {
            if (!indexByKey.ContainsKey(key))
                throw new InvalidOperationException(
                    $"CSV header に必要列が見つかりません: {key}. ヘッダ: {string.Join(",", header)}");
        }

        var result = new List<string[]>();

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);

            int need = indexByKey.Values.Max();
            if (cols.Length <= need) continue;

            bool Cheker = RequiredKeys.All(key =>
            {
                int idx = indexByKey[key];
                return MatchCell(state, key, cols[idx],
                ref level4Count,ref emo5Count);
            });

            if (Cheker) result.Add(cols);
        }

        int iSource = 16;


        string randomItem = "";
        if (result.Count != 0)
        {
            List<string> sourceList = result
            .Select(cols => cols[iSource]).ToList();

            randomItem = sourceList[UnityEngine.Random.Range(0, sourceList.Count)];
        }

        return randomItem;
    }


    public static List<string[]> returnCSV(HintSelectionState state, string CsvPath, out int level4MatchCount,
    out int emo5MatchCount)
    {
        level4MatchCount = 0; 
        emo5MatchCount = 0;

        var lines = File.ReadAllLines(CsvPath);

        if (lines.Length == 0) return new List<string[]>();


        var header = SplitCsvLine(lines[0]);
        var indexByKey = ResolveIndexes(header);

        var allKeys = (HintKey[])Enum.GetValues(typeof(HintKey));

        var result = new List<string[]>();

        for (int i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            var cols = SplitCsvLine(line);

            int level4Count = 0;
            int emo5Count = 0;
            bool ok = true;

            foreach (var key in allKeys)
            {

                if (!indexByKey.TryGetValue(key, out int idx))
                    continue;

                if (idx < 0 || idx >= cols.Length) { ok = false; break; }

                if (!MatchCell(state, key, cols[idx], ref level4Count, ref emo5Count))
                {
                    ok = false;
                    break;
                }
            }
            if (ok) result.Add(cols);

            level4MatchCount += level4Count;
            emo5MatchCount += emo5Count;
        }

        return result;
    }

    private static Dictionary<HintKey, int> ResolveIndexes(string[] header)
    {
        var dict = new Dictionary<HintKey, int>();

        for (int i = 0; i < header.Length; i++)
        {
            var name = header[i].Trim();
            if (HeaderMap.TryGetValue(name, out var key))
            {

                if (!dict.ContainsKey(key))
                    dict.Add(key, i);
            }
        }

        return dict;
    }

    private static string[] SplitCsvLine(string line) => line.Split(',');

    public static bool MatchCell(HintSelectionState state, HintKey key, string csvCell,ref int level4MatchCount,
    ref int emo5MatchCount)
    {
        if (state.IsWildcard(key)) return true;

        csvCell = csvCell.Trim();

        if (state.TryGetLevel4(key, out var lv))
        {
            if (Enum.TryParse(csvCell, true, out Level4 v) && v == lv)
            {
                level4MatchCount++;
                return true;
            }
            return false;
        }
        if (state.TryGetEmo5(key, out var emo))
        {
            if (Enum.TryParse(csvCell, true, out Emo5 v) && v == emo)
            {
                emo5MatchCount++;
                return true;
            }
            return false;
        }
        return false;
    }

}
