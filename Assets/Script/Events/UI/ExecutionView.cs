using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MusicMap.Events
{
    public sealed class ExecutionView : MonoBehaviour
    {
        public GameObject panel;
        public TMP_Text log;
        public TMP_Text dice;
        public TMP_Text rewards;
        public TMP_Text message;
        public Image eventImage;
        public Button continueButton;
        private string displayedInstanceId;
        public event Action ContinueRequested;
        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => ContinueRequested?.Invoke());
            Hide();
        }
        public void Show(EventInstance instance, EventDefinitionSO definition, EventPresentation presentation)
        {
            if (panel != null) panel.SetActive(true);
            if (log != null)
            {
                if (displayedInstanceId != instance.instanceId) log.text = definition.displayName + "\n\n";
                log.text += (presentation?.text ?? "") + "\n\n";
            }
            displayedInstanceId = instance.instanceId;
            if (eventImage != null) { eventImage.sprite = presentation?.image; eventImage.enabled = eventImage.sprite != null; }
            if (dice != null) dice.text = instance.hasJudgement ? $"ダイス: {string.Join(", ", instance.diceResults)}\n合計: {instance.judgeTotal}\n{instance.resolvedOutcomeCode}" : "判定前";
            if (message != null) message.text = "";
            var outcome = definition.outcomes.FirstOrDefault(o => o.outcomeCode == instance.resolvedOutcomeCode);
            if (rewards != null) rewards.text = outcome == null ? "" : string.Join("\n", outcome.effects.Select(Describe))
                + "\n" + string.Join("\n", outcome.followUps.Select(f => $"後続: {f.definition?.displayName} ({f.startMode})"));
        }
        private static string Describe(EventEffect effect) => effect.kind switch
        {
            EventEffectKind.GrantGold => $"Gold +{effect.amount}",
            EventEffectKind.ConsumeGold => $"Gold -{effect.amount}",
            EventEffectKind.GrantEquip => $"装備: {effect.equip?.name} ×{effect.amount}",
            EventEffectKind.GrantAbility => $"能力: {effect.ability?.name} ×{effect.amount}",
            EventEffectKind.GrantHint => $"ヒント: {effect.hint?.name} ×{effect.amount}",
            EventEffectKind.ChangeParameter => $"{effect.parameter}: {effect.amount:+0;-0;0}",
            _ => effect.kind.ToString()
        };
        public void ShowError(string text) { if (message != null) message.text = text + "\n接続・状態を確認後、続行で再試行できます。"; }
        public void Hide() { if (panel != null) panel.SetActive(false); displayedInstanceId = null; }
    }
}
