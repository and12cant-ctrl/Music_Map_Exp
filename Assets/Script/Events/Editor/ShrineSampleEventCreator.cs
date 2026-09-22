using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MusicMap.Events.Editor
{
    public static class ShrineSampleEventCreator
    {
        public const string SampleFolder = "Assets/イベント/サンプル";
        public const string EventPath = SampleFolder + "/神社の依頼（簡易サンプル）.asset";
        public const string CatalogPath = SampleFolder + "/神社サンプルイベントカタログ.asset";

        private const string CategoryCatalogPath = "Assets/イベント/イベントカテゴリ.asset";
        private const string LegacyShrineCategoryPath = "Assets/キャラ/イベント/イベント/神社.asset";
        [MenuItem("Tools/Events/Create Shrine Sample Event")]
        public static void CreateFromMenu()
        {
            CreateOrSelect(true);
        }

        public static void CreateForBatch()
        {
            CreateOrSelect(false);
        }

        private static void CreateOrSelect(bool showDialog)
        {
            var existingEvent = AssetDatabase.LoadAssetAtPath<EventDefinitionSO>(EventPath);
            var existingCatalog = AssetDatabase.LoadAssetAtPath<EventCatalogSO>(CatalogPath);
            if (existingEvent != null || existingCatalog != null)
            {
                if (existingEvent != null && existingCatalog != null)
                {
                    Selection.activeObject = existingEvent;
                    EditorGUIUtility.PingObject(existingEvent);
                    if (showDialog) EditorUtility.DisplayDialog("Shrine Sample Event", "The sample event already exists. It has been selected in the Project window.", "OK");
                }
                else
                {
                    Debug.LogError("[ShrineSampleEventCreator] Only one sample asset exists. Nothing was overwritten. Resolve the partial sample before running the creator again.");
                }
                return;
            }

            var categoryCatalog = AssetDatabase.LoadAssetAtPath<EventCategoryCatalogSO>(CategoryCatalogPath);
            if (categoryCatalog == null || !categoryCatalog.TryGetByStrictDisplayName("神社", out var shrineCategory) || shrineCategory.IsDeprecated)
            {
                Debug.LogError("[ShrineSampleEventCreator] An active '神社' category was not found in the expected event category catalog.");
                return;
            }

            var legacyShrineCategory = AssetDatabase.LoadAssetAtPath<EventCategory>(LegacyShrineCategoryPath);
            var shrineIcon = legacyShrineCategory != null ? legacyShrineCategory.catIcon : null;
            if (shrineIcon == null)
            {
                Debug.LogError("[ShrineSampleEventCreator] The existing shrine icon could not be resolved from the legacy shrine category.");
                return;
            }

            EnsureFolder("Assets", "イベント");
            EnsureFolder("Assets/イベント", "サンプル");

            EventDefinitionSO definition = null;
            EventCatalogSO catalog = null;
            try
            {
                definition = EventDatabaseWindow.CreateAsset(EventPath, null, shrineCategory.Id);
                definition.eventCode = "SAMPLE_SHRINE_REQUEST_001";
                definition.displayName = "神社の依頼（簡易サンプル）";
                definition.enabled = true;
                definition.tags = new List<string> { "サンプル", "神社" };
                definition.developerMemo =
                    "現行イベントシステムだけで動作を確認する簡易サンプル。" +
                    "施設好感度、施設ごとの前提イベント成功履歴、夜限定条件は未実装のため使用していません。";
                definition.sortOrder = 10000;
                definition.priority = 0;

                definition.appearance = new EventAppearanceDefinition
                {
                    mapIcon = shrineIcon,
                    mapColorKey = string.Empty,
                    generateOnMap = true,
                    requiresUnlock = false,
                    requiredPlacements = 1,
                    weight = 0,
                    backgroundCategories = new List<string>(),
                    conditions = new List<EventCondition>
                    {
                        new EventCondition { kind = EventConditionKind.Always }
                    }
                };

                definition.reservation = new EventReservationDefinition
                {
                    triggerMode = EventTriggerMode.Scheduled,
                    requiredPhaseCount = 1,
                    reservationPresentation = new EventPresentation
                    {
                        text = "神社の境内で、神主から小さな依頼を持ちかけられた。人物カードを1枚割り当てて、調査を予約します。",
                        image = shrineIcon
                    },
                    cardSlots = new List<CardSlotDefinition>
                    {
                        new CardSlotDefinition
                        {
                            label = "調査担当",
                            acceptedType = CardType.Chara,
                            required = true,
                            amount = 1
                        }
                    },
                    requirements = new List<EventRequirement>
                    {
                        new EventRequirement { parameter = EventParameter.Appeal, minimum = 1 }
                    }
                };

                definition.execution = new EventExecutionDefinition
                {
                    executionType = EventExecutionType.Narrative,
                    beforeJudge = new EventPresentation
                    {
                        text = "約束の時間になった。神主の依頼を確かめるため、境内を調べ始めた。",
                        image = shrineIcon
                    },
                    judge = new EventJudgeDefinition
                    {
                        useDice = true,
                        parameter = EventParameter.Appeal,
                        diceCount = 1,
                        diceSides = 6,
                        target = 4,
                        modifier = 0
                    },
                    externalKey = string.Empty
                };

                definition.outcomes = new List<EventOutcomeDefinition>
                {
                    new EventOutcomeDefinition
                    {
                        outcomeCode = "SUCCESS",
                        isDefault = false,
                        conditions = new List<EventCondition>
                        {
                            new EventCondition { kind = EventConditionKind.JudgeSucceeded }
                        },
                        presentation = new EventPresentation
                        {
                            text = "手がかりを見つけ、神主の依頼を無事に果たした。お礼として10ゴールドを受け取った。",
                            image = shrineIcon
                        },
                        effects = new List<EventEffect>
                        {
                            new EventEffect { kind = EventEffectKind.GrantGold, amount = 10 }
                        },
                        followUps = new List<EventFollowUp>()
                    },
                    new EventOutcomeDefinition
                    {
                        outcomeCode = "FAILURE",
                        isDefault = true,
                        conditions = new List<EventCondition>(),
                        presentation = new EventPresentation
                        {
                            text = "手がかりは見つからなかった。今回は報酬を得られなかった。",
                            image = shrineIcon
                        },
                        effects = new List<EventEffect>(),
                        followUps = new List<EventFollowUp>()
                    }
                };

                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);

                catalog = ScriptableObject.CreateInstance<EventCatalogSO>();
                catalog.name = "神社サンプルイベントカタログ";
                catalog.categoryCatalog = categoryCatalog;
                catalog.definitions = new List<EventDefinitionSO> { definition };
                AssetDatabase.CreateAsset(catalog, CatalogPath);
                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();

                var issues = EventValidator.Validate(new[] { definition }, catalog);
                if (issues.Count > 0)
                {
                    foreach (var issue in issues) Debug.LogError("[ShrineSampleEventCreator] " + issue, issue.asset);
                    throw new InvalidOperationException($"The generated sample has {issues.Count} validation issue(s).");
                }

                Selection.activeObject = definition;
                EditorGUIUtility.PingObject(definition);
                Debug.Log($"[ShrineSampleEventCreator] Created and validated the sample event at '{EventPath}'.", definition);
                if (showDialog) EditorUtility.DisplayDialog("Shrine Sample Event", "The sample event and isolated sample catalog were created and passed EventValidator.", "OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (catalog != null) AssetDatabase.DeleteAsset(CatalogPath);
                if (definition != null) AssetDatabase.DeleteAsset(EventPath);
                AssetDatabase.Refresh();
            }
        }

        private static void EnsureFolder(string parent, string child)
        {
            var path = parent + "/" + child;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }
    }
}
