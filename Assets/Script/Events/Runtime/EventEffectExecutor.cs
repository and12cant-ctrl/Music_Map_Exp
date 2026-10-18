using System;
using System.Linq;

namespace MusicMap.Events
{
    public sealed class EventEffectExecutor
    {
        private readonly PartyManager party;
        private readonly EventManager manager;
        private readonly EventCardRegistry cards;
        private readonly IEventWorldState world;
        private readonly Func<EventExecutionType, IEventExecutionHandler> handlers;

        public EventEffectExecutor(EventManager manager, PartyManager party, EventCardRegistry cards,
            IEventWorldState world, Func<EventExecutionType, IEventExecutionHandler> handlers)
        { this.manager = manager; this.party = party; this.cards = cards; this.world = world; this.handlers = handlers; }

        public bool TryApplyOutcome(EventDefinitionSO definition, EventInstance instance, EventOutcomeDefinition outcome,
            string operationId, Action<bool, string> completion, out string error)
        {
            if (instance.appliedEffectCount < outcome.effects.Count)
                return TryApply(definition, instance, outcome.effects[instance.appliedEffectCount], operationId, completion, out error);
            var effect = outcome.typedEffects[instance.appliedEffectCount - outcome.effects.Count];
            if (effect == null) return EventManager.Fail("Missing typed effect.", out error);
            bool success = effect.Apply(new EventEffectContext { manager = manager, definition = definition, instance = instance, executor = this, party = party, operationId = operationId }, out error);
            if (success) completion(true, null);
            return success;
        }

        // A successful step advances the journal in EventRunner. A failed step stays retryable.
        public bool TryApply(EventDefinitionSO definition, EventInstance instance, EventEffect effect,
            string operationId, Action<bool, string> completion, out string error)
        {
            error = null;
            if (effect == null) return EventManager.Fail("Null event effect.", out error);
            if (effect.kind == EventEffectKind.StartBattle)
            {
                var battle = handlers?.Invoke(EventExecutionType.Encounter);
                if (battle == null) return EventManager.Fail("Encounter handler is not connected.", out error);
                return battle.TryBegin(definition, instance.Copy(), effect.key, operationId, completion, out error);
            }
            bool success;
            switch (effect.kind)
            {
                case EventEffectKind.SetFlag:
                    if (world == null) return EventManager.Fail("Flag system is not connected.", out error);
                    success = world.TrySetFlag(effect.key, effect.flagValue, operationId, out error);
                    break;
                case EventEffectKind.UnlockEvent: success = manager.Unlock(effect.eventDefinition, out error); break;
                case EventEffectKind.ReserveEvent: success = manager.StartFollowUp(instance, effect.eventDefinition, EventFollowUpMode.Reserve, out error); break;
                default:
                    if (party?.Runtime == null) return EventManager.Fail("PartyManager is not initialized.", out error);
                    switch (effect.kind)
                    {
                        case EventEffectKind.GrantGold:
                            if (effect.amount < 0 || (long)party.Runtime.Gold + effect.amount > int.MaxValue) return EventManager.Fail("Invalid gold reward.", out error);
                            party.AddGold(effect.amount); break;
                        case EventEffectKind.ConsumeGold:
                            if (effect.amount < 0 || !party.SpendGold(effect.amount)) return EventManager.Fail("Insufficient gold or invalid cost.", out error);
                            break;
                        case EventEffectKind.GrantAbility:
                            if (effect.ability == null || effect.amount < 1 || effect.amount > 10000) return EventManager.Fail("Ability reward or amount is invalid.", out error);
                            for (int count = 0; count < effect.amount; count++) party.AddAbility(effect.ability); break;
                        case EventEffectKind.GrantEquip:
                            if (effect.equip == null || effect.amount < 1 || effect.amount > 10000) return EventManager.Fail("Equipment reward or amount is invalid.", out error);
                            for (int count = 0; count < effect.amount; count++) party.AddEquip(effect.equip); break;
                        case EventEffectKind.GrantHint:
                            if (effect.hint == null || effect.amount < 1 || effect.amount > 10000) return EventManager.Fail("Hint reward or amount is invalid.", out error);
                            if (!string.IsNullOrEmpty(effect.hint.PersistentId)) manager.Progress.GrantHint(effect.hint.PersistentId, 1);
                            for (int count = 0; count < effect.amount; count++) party.AddHint(effect.hint); break;
                        case EventEffectKind.ChangeParameter:
                            var characters = cards?.AssignedCharacters(instance.assignedCardIds).ToArray();
                            if (characters == null || characters.Length == 0) return EventManager.Fail("No assigned character for parameter change.", out error);
                            foreach (var character in characters) EventCardRegistry.ChangeParameter(character, effect.parameter, effect.amount);
                            break;
                        default: return EventManager.Fail($"Unsupported effect: {effect.kind}", out error);
                    }
                    success = true;
                    break;
            }
            if (success) completion(true, null);
            return success;
        }
    }
}
