using System.IO;
using UnityEngine;

public class MusicXmlToStaffImporter : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TextAsset musicXmlTextAsset; // まずはこれで簡単に
    [SerializeField] private string musicXmlFilePath;     // ファイルパスでもOK（PC上）

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
        Import();
    }
    public void Import()
    {
        string xml = null;

        if (musicXmlTextAsset != null) xml = musicXmlTextAsset.text;
        else if (!string.IsNullOrEmpty(musicXmlFilePath) && File.Exists(musicXmlFilePath))
            xml = File.ReadAllText(musicXmlFilePath);

        if (string.IsNullOrEmpty(xml))
        {
            Debug.LogError("MusicXML が指定されていません（TextAsset or FilePath）");
            return;
        }

        var events = MusicXmlMinimalParser.Parse(xml, part, voice);

        var applier = new ScoreEventApplier(nodeCheak, staffRenderer);
        var coord = new StaffCoordinateConverter(staffRect, beatsPerBar, pixelsPerBar, scaleSteps);

        int i = 0;
        foreach (var e in events)
        {
            if (skipRests && e.pitch == "REST") continue;

            var pos = coord.ToAnchoredPosition(e);
            applier.ApplyPlace(e, pos, $"xml_{i++}");
        }

        Debug.Log($"Import done: placed {i} notes.");
    }
}
