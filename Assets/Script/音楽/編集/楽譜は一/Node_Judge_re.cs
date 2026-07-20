using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class Node_judge_re : MonoBehaviour, IPointerClickHandler
{
    [Header("UI")]
    [SerializeField] private RectTransform staffRect;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Camera uiCamera;

    [Header("Logic")]
    [SerializeField] private Node_Place nodeCheak;
    [SerializeField] public SimpleStaffRenderer staffRenderer;
    [SerializeField] public StaffCoordinateConverter _coord;
    [SerializeField] public TabOverLayCloser _closer;

    [Header("Score Settings")]
    public float beat = 4.0f;
    private int scale = 21;
    //public int scaleSteps = 32;
    private float beatsPerBar = 4f;
    public Part part = Part.A;
    public voice voice = default;
    public float nodeLength = 0.5f;
    public int currentBar = 1;

    [Header("UI Layout")]
    public float pixelsPerBar = 330f;

    [Header("Pitch")]
    public harfNode hafeN = harfNode.None;
    private readonly string[] Gcode = {"G", "A", "B", "C", "D", "E", "F", };

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
        _coord = new StaffCoordinateConverter(staffRect, beatsPerBar, pixelsPerBar, scale);
        _applier = new ScoreEventApplier(nodeCheak, staffRenderer,_coord);
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
        if (!converter.TryConvert(eventData, part, voice, out var scoreEvent))
            return;

        string nodeName = $"{scoreEvent.bar}_{scoreEvent.pitch}_{scoreEvent.start}_{scoreEvent.duration}";
        switch (editMode)
        {
            case EditMode.Place:
                _applier.ApplyPlace(scoreEvent, nodeName);
                break;

            case EditMode.Delete:
                _applier.ApplyDelete(scoreEvent);
                break;
        }
    }
    #region enum‘g
    public enum LengthNode
    {
        Harf, One, Two, All
    }
    public enum harfNode
    {
        None, Shape, Flat
    }
    public enum EditMode
    {
        Place, Delete, All
    }
    #endregion

}
