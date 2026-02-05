using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using System.Linq;

public class MusicXmlToStaffImporter : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TextAsset musicXmlTextAsset; // まずはこれで簡単に
    private string musicXmlFilePath ="";     // ファイルパスでもOK（PC上）

    [Header("Targets")]
    [SerializeField] private RectTransform staffRect;
    [SerializeField] private Node_Place nodeCheak;
    [SerializeField] private SimpleStaffRenderer staffRenderer;

    [Header("Mapping")]
    [SerializeField] private Part part = Part.A;
    [SerializeField] private voice voice = default;
    [SerializeField] private float beatsPerBar = 4f;
    [SerializeField] private float pixelsPerBar = 330f;
    [SerializeField] private int scaleSteps = 20;

    [Header("Options")]
    [SerializeField] private bool skipRests = true;
    private void Start()
    {
        ImportFromMusic21(musicXmlFilePath);
    }
    public void ImportFromMusic21(string musicXmlPath)
    {
        // 1) Pythonを呼んでJSONをもらう（ここはあなたのPythonReturnに置換）
        // string json = PythonReturn.Run("music21_to_scoreevents.py", musicXmlPath);
        string json = "";

        var payload = JsonUtility.FromJson<ScoreEventsPayload>(json);
        var events = payload.events.Select(ToScoreEvent).ToList();

        // 2) beatsPerBar は Pythonの結果を採用（ここ超重要）
        var coord = new StaffCoordinateConverter(staffRect, payload.beatsPerBar, pixelsPerBar, scaleSteps);
        var applier = new ScoreEventApplier(nodeCheak, staffRenderer);

        int placed = 0;
        foreach (var e in events)
        {
            if (skipRests && e.pitch == "REST") continue;

            var pos = coord.ToAnchoredPosition(e);
            applier.ApplyPlace(e, pos, $"m21_{placed++}");
        }

        Debug.Log($"Import done (music21): placed {placed} notes. beatsPerBar={payload.beatsPerBar}, total={events.Count}");
    }
    public static ScoreEvent ToScoreEvent(ScoreEventDto d)
    {
        return new ScoreEvent
        {
            part = Enum.Parse<Part>(d.part),
            voice = Enum.Parse<voice>(d.voice),
            pitch = d.pitch,
            duration = d.duration,
            bar = d.bar,
            start = d.start
        };
    }
    [Serializable]
    public class ScoreEventDto
    {
        public string part;   // "A".."D"
        public string voice;  // "Up"/"Middle"/"Down"
        public string pitch;
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
}
