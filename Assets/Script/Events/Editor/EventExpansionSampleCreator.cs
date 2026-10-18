using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MusicMap.Events.Editor
{
    public static class EventExpansionSampleCreator
    {
        public const string Folder = "Assets/EventS/ExtensionSample";
        [MenuItem("Tools/Events/Create Extension Sample")]
        public static void Create()
        {
            if (!AssetDatabase.IsValidFolder("Assets/EventS")) AssetDatabase.CreateFolder("Assets", "EventS");
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/EventS", "ExtensionSample");
            if (AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(Folder + "/PhaseSample.asset") != null) return;
            T Asset<T>(string name) where T : ScriptableObject
            {
                var a = ScriptableObject.CreateInstance<T>(); a.name = name;
                // CreateAsset serializes before OnValidate can supply a missing identity.
                if (a is EventDefinitionSO) typeof(EventDefinitionSO).GetField("persistentId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(a, Guid.NewGuid().ToString("N"));
                AssetDatabase.CreateAsset(a, Folder + "/" + name + ".asset"); return a;
            }
            var flag = Asset<FlagDefinitionSO>("SampleCompleted"); flag.displayName = "サンプル完了";
            var blocked = Asset<FlagDefinitionSO>("SampleBlocked"); blocked.displayName = "サンプル発生禁止";
            var hint = Asset<HintData>("SampleHint"); hint.displayName = "テスト用ヒント";
            var pool = Asset<HintPoolSO>("SampleCasePool"); pool.displayName = "テスト事件"; pool.hints.Add(hint);
            var d = Asset<EventDefinitionSO>("PhaseSample"); d.eventCode = "EXTENSION_PHASE_SAMPLE"; d.displayName = "拡張イベント動作確認"; d.useExtendedLifecycle = true;
            d.developerMemo = "テスト用。成功後1 Phaseで同じ定義が再出現。Startには人物カード1枚。既存の本番Catalogへ自動登録しません。";
            d.reservation.reservationPresentation.text = "人物カードを配置して Start。1 Phase後に判定します。";
            d.reservation.cardSlots.Add(new CardSlotDefinition { label = "参加者", slotType = SlotType.Required });
            d.execution.beforeJudge.text = "参加者を確定しました。結果へ進みます。";
            d.execution.judge.useDice = false; d.execution.judge.target = 0;
            d.extension.randomHintPool = pool;
            d.extension.conditions.Add(new OccurrenceCondition { query = new EventQuery { source = ConditionSource.Flag, flag = blocked, boolValue = false } });
            d.extension.success.typedEffects.Add(new HintEffect { fixedHint = hint });
            d.extension.success.typedEffects.Add(new FlagEffect { flag = flag, value = true });
            d.extension.success.followUps.Add(new EventFollowUp { definition = d, delayPhaseCount = 1 });
            d.extension.pointConditions.Add(new PointCondition { query = new EventQuery { source = ConditionSource.World, phase = WorldPhase.Night }, addPoint = 4 });
            d.extension.success.presentation.text = "SUCCESS：ヒント獲得。1 Phase後に同じイベントが再出現します。";
            d.extension.failure.presentation.text = "FAILURE：テスト失敗。";
            d.extension.expired.presentation.text = "EXPIRED：Start前に期限切れ。参加者なし。";
            var categories = Asset<EventCategoryCatalogSO>("SampleCategories"); categories.EditorTryAdd("検証用", "", 0, out var category, out _);
            typeof(EventDefinitionSO).GetField("categoryId", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, category.Id);
            typeof(EventDefinitionSO).GetField("categoryMigrationState", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(d, EventCategoryMigrationState.Migrated);
            var catalog = Asset<EventCatalogSO>("SampleCatalog"); catalog.categoryCatalog = categories; catalog.definitions.Add(d);
            // Reuse an existing Sprite to make the sample visible on the map.
            foreach (string guid in AssetDatabase.FindAssets("t:Sprite"))
            {
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset is Sprite sprite) { d.appearance.mapIcon = sprite; break; }
                if (d.appearance.mapIcon != null) break;
            }
            d.appearance.requiredPlacements = 1;
            foreach (var asset in new UnityEngine.Object[] { flag, blocked, hint, pool, d, categories, catalog }) EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            Debug.Log("[EventExpansionSample] Created " + Folder);
        }
    }
}
