using System;
using UnityEngine;
using UnityEngine.EventSystems;
using static Node_judge;

public class Node_judge_re : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private RectTransform staffRect;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera uiCamera;

    [Header("Logic")]
    [SerializeField] private Node_Place nodeCheak;
    [SerializeField] private SimpleStaffRenderer staffRenderer;

    [Header("Score Settings")]
    public float beat = 4.0f;
    public int scale = 20;
    public Part part = Part.A;
    public voice voice = default;
    public float nodeLength = 0.5f;
    public int currentBar = 1;

    [Header("UI Layout")]
    public float pixelsPerBar = 330f;

    [Header("Pitch")]
    public harfNode hafeN = harfNode.None;
    private readonly string[] Gcode = { "A", "B", "C", "D", "E", "F", "G" };

    [Header("State")]
    [SerializeField] private EditMode _editMode;

    [HideInInspector] public RectTransform NodePrehab;
    public EditMode editMode
    {
        get => _editMode;
        set
        {
            if (_editMode == value) return;
            _editMode = value;
        }
    }

    private ScoreEventApplier _applier;

    private void Awake()
    {
        if (nodeCheak == null) Debug.LogError("nodeCheak (Node_Place) is null");
        if (staffRenderer == null) Debug.LogWarning("staffRenderer is null (UI反映しないならOK)");

        _applier = new ScoreEventApplier(nodeCheak, staffRenderer,);
        NodePrehab = staffRenderer.nodePrefab;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        var converter = new PointerToScoreEventConverter(
            staffRect: staffRect,
            canvas: canvas,
            uiCamera: uiCamera,
            beatsPerBar: beat,
            scaleSteps: scale,
            nodeLength: nodeLength,
            currentBar: currentBar,
            pixelsPerBar: pixelsPerBar,
            halfNode: hafeN,
            gcode: Gcode
        );
        //Debug.Log($"[OnPointerClick ENTER] this={GetInstanceID()} editMode={editMode}");
        if (!converter.TryConvert(eventData, part, voice, out var scoreEvent, out var anchoredPos))
            return;

        // ノード名（元の nameNumber を踏襲したいならここでインクリメント等）
        string nodeName = "1";
        //Debug.Log($"[OnPointerClick BEFORE SWITCH] this={GetInstanceID()} editMode={editMode}");
        switch (editMode)
        {
            case EditMode.Place:
                _applier.ApplyPlace(scoreEvent, anchoredPos, nodeName);
                break;

            case EditMode.Delete:
                _applier.ApplyDelete(scoreEvent, anchoredPos);
                break;
        }
    }
}
