using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(RectTransform))]
public class NoteDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public StaffSnapper staff;       // 対象の五線
    public bool quantizeX = true;    // 横方向の量子化ON/OFF
    public float beatWidth = 32f;    // 1拍のUI幅(px)
    public Vector2 dragOffset;       // つまんだ位置の相対オフセット

    private RectTransform _rect;
    private Canvas _rootCanvas;

    public int StepIndex { get; private set; } // スナップ後の段数
    public int Midi { get; private set; }      // 確定したMIDI（必要なら）

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _rootCanvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, eventData.position, _rootCanvas.worldCamera, out var local);
        dragOffset = _rect.pivot * _rect.rect.size - local;
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveToPointer(eventData);
        TrySnap(false);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        MoveToPointer(eventData);
        TrySnap(true);
        // ここで確定イベントを飛ばしてもよい（例：OnNoteSnapped?.Invoke(Midi, StepIndex, BeatX);）
    }

    private void MoveToPointer(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            staff.Rect, eventData.position, _rootCanvas.worldCamera, out var local);

        var target = local + dragOffset; // つまんだ位置の補正
        // Xは自由に動かす（量子化は後段）
        _rect.anchoredPosition = new Vector2(target.x, target.y);
    }

    private void TrySnap(bool finalize)
    {
        var pos = _rect.anchoredPosition;

        // Y方向スナップ判定
        int nearest = staff.YToNearestStepIndex(pos.y);
        float snapY = staff.StepIndexToY(nearest);
        float dy = Mathf.Abs(snapY - pos.y);

        if (dy <= staff.snapRadius)
        {
            pos.y = snapY;
            StepIndex = nearest;

            // 例：必要なら StepIndex→MIDI 変換をここで行う（逆写像用テーブルを持つ）
            // Midi = staff.StepIndexToMidi(StepIndex);

            // X方向の量子化
            if (quantizeX)
            {
                float beats = pos.x / beatWidth;
                float snappedBeats = Mathf.Round(beats);
                pos.x = snappedBeats * beatWidth;
            }

            _rect.anchoredPosition = pos;
        }
        else if (finalize)
        {
            // 最終確定時のみ“吸い寄せ”を強制するならココで pos.y = snapY;
            // （UI/操作感の方針に合わせて）
        }
    }
}