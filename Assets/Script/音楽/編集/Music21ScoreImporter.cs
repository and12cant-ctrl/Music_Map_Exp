using Newtonsoft.Json;
using NUnit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[Serializable]
public class ScoreEventDto
{
    public string part;   // "A".."D"
    public string voice;  // "Up"（今は固定）
    public string pitch;  // "C#4" or "REST"
    public float duration;
    public int bar;
    public float start;
}

[Serializable]
public class ScoreEventsPayload
{
    public float beatsPerBar;
    public List<ScoreEventDto> events;
}

public class Music21ScoreImporter : MonoBehaviour
{
    [Header("Scene refs")]
    [SerializeField] private RectTransform staffRect;
    [SerializeField] private Node_Place nodeCheak;
    [SerializeField] private SimpleStaffRenderer staffRenderer;
    private string pythonExePath = Path.Combine(FluidSynthPaths.micromambaroot, "envs", "unitypy","python.exe");

    private string scriptPath = Path.Combine(FluidSynthPaths.scriptPy, "music21_to_scoreevents.py");

     private float pixelsPerBar = 330f;
    private int scaleSteps = 20;

    [Header("Options")]
    [SerializeField] private bool skipRests = true;

    [Header("Input")]
    [SerializeField]public string musicXmlFilePath; // 読みたいMusicXMLのフルパス

    private void Start()
    {
        ImportWithMusic21();
    }
    public void ImportWithMusic21()
    {
        #region MyRegion
        if (string.IsNullOrEmpty(musicXmlFilePath) || !File.Exists(musicXmlFilePath))
        {
            Debug.LogError($"MusicXML file not found: {musicXmlFilePath}");
            return;
        }

        if (string.IsNullOrEmpty(pythonExePath) || !File.Exists(pythonExePath))
        {
            Debug.LogError($"pythonExePath not found: {pythonExePath}");
            return;
        }

        if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
        {
            Debug.LogError($"scriptPath not found: {scriptPath}");
            return;
        }

        #endregion

        // python script.py music.xml
        string args = $"\"{scriptPath}\" \"{musicXmlFilePath}\"";
        var r = PythonReturn.Run(pythonExePath, args, timeoutMs: 120_000);

        if (r.TimedOut)
        {
            PythonReturn.PyLog.LogStderr(r.Stderr, "M21", "TimedOut");
            Debug.LogError("music21 timed out");
            return;
        }
        if (r.ExitCode != 0)
        {
            PythonReturn.PyLog.LogStderr(r.Stderr, "M21", $"ExitCode={r.ExitCode}");
            Debug.LogError("music21 failed");
            return;
        }
        if (!string.IsNullOrWhiteSpace(r.Stderr))
        {
            // 警告がstderrに出ることがある（必要ならログ）
            PythonReturn.PyLog.LogStderr(r.Stderr, "M21", "NonEmptyStderr");
        }

        ScoreEventsPayload payload;
        try
        {
            payload = JsonConvert.DeserializeObject<ScoreEventsPayload>(r.Stdout);
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON parse failed: {ex}\n---stdout---\n{r.Stdout}");
            return;
        }

        if (payload?.events == null || payload.events.Count == 0)
        {
            Debug.LogWarning($"music21 returned 0 events. beatsPerBar={payload?.beatsPerBar}");
            return;
        }

        //  beatsPerBar は Python結果を採用（ズレ防止）
        var coord = new StaffCoordinateConverter(staffRect, payload.beatsPerBar, pixelsPerBar, scaleSteps);
        var applier = new ScoreEventApplier(nodeCheak, staffRenderer);

        int placed = 0;
        int minBar = payload.events.Min(E => E.bar);
        foreach (var d in payload.events)
        {
            if (skipRests && d.pitch == "REST") continue;

            // ScoreEvent（あなたのstruct）に変換
            var e = new ScoreEvent
            {
                part = Enum.Parse<Part>(d.part),
                voice = Enum.Parse<voice>(d.voice), // 今は "Up" 固定
                pitch = d.pitch,
                duration = d.duration,
                bar = d.bar - minBar +1,
                start = d.start
            };

            var pos = coord.ToAnchoredPosition(e);
            applier.ApplyPlace(e, pos, $"{placed++}");
        }

        Debug.Log($"Import done (music21): placed {placed} notes. total={payload.events.Count}, beatsPerBar={payload.beatsPerBar}");
    }
}
