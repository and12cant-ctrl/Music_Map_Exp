using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MusicMap.Events.Editor
{
    public static class EventUiPreview
    {
        [MenuItem("Tools/Events/Capture UI Preview")]
        public static void Capture()
        {
            const string prefabPath = "Assets/Prehab/EventSystemUI.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) throw new System.InvalidOperationException("Build EventSystemUI.prefab first.");
            var scene = EditorSceneManager.NewPreviewScene();
            var definition = ScriptableObject.CreateInstance<EventDefinitionSO>();
            RenderTexture target = null;
            try
            {
                var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.transform.position = new Vector3(0, 0, -10);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.12f, .15f, .2f);
                camera.orthographic = true;
                camera.orthographicSize = 540;
                camera.cullingMask = 1 << 31;
                target = new RenderTexture(1920, 1080, 24);
                camera.targetTexture = target;
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var canvasRect = root.GetComponent<RectTransform>();
                canvasRect.sizeDelta = new Vector2(1920, 1080);
                canvasRect.position = Vector3.zero;
                canvasRect.localScale = Vector3.one;
                var icon = AssetDatabase.FindAssets("t:EventCategory").Select(g => AssetDatabase.LoadAssetAtPath<EventCategory>(AssetDatabase.GUIDToAssetPath(g))).Where(c => c != null).Select(c => c.catIcon).FirstOrDefault(s => s != null);
                definition.displayName = "イベント表示の確認";
                definition.reservation.requiredPhaseCount = 2;
                definition.reservation.reservationPresentation.text = "仲間のカードを配置して予約します。\n必要な能力と時間帯を確認してください。\nこの文章は画面確認用です。";
                definition.reservation.reservationPresentation.image = icon;
                for (int index = 0; index < 6; index++) definition.reservation.cardSlots.Add(new CardSlotDefinition { label = "カード " + (index + 1), required = index < 3 });
                definition.reservation.requirements.Add(new EventRequirement { parameter = EventParameter.Appeal, minimum = 8 });
                definition.reservation.requirements.Add(new EventRequirement { parameter = EventParameter.Mystery, minimum = 4 });
                var reservation = root.GetComponent<ReservationView>();
                if (!reservation.Show(definition, out var error)) throw new System.InvalidOperationException(error);
                root.GetComponent<EventScheduleView>().Show(new[] { ("preview", "探索イベント / Reserved / 残り 2 時間帯", true) });
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer = 31;
                Directory.CreateDirectory("Logs/EventSystemVerification");
                Save(camera, target, "Logs/EventSystemVerification/reservation-preview.png");
                reservation.panel.SetActive(false);
                var instance = new EventInstance { instanceId = "PREVIEW" };
                var execution = root.GetComponent<ExecutionView>();
                execution.Show(instance, definition, new EventPresentation { text = "仲間たちは周囲を調べ始めました。\nカードの能力値とダイスで結果を判定します。", image = icon });
                instance.hasJudgement = true; instance.diceResults.Add(5); instance.judgeTotal = 13; instance.resolvedOutcomeCode = "SUCCESS";
                definition.outcomes[0].effects.Add(new EventEffect { kind = EventEffectKind.GrantGold, amount = 20 });
                execution.Show(instance, definition, new EventPresentation { text = "判定に成功しました。\n報酬を確認して「続行」を押すと結果を適用します。", image = icon });
                Save(camera, target, "Logs/EventSystemVerification/execution-preview.png");
                Debug.Log("[EventUiPreview] Saved reservation and execution previews under Logs/EventSystemVerification.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Object.DestroyImmediate(definition);
                if (target != null) { target.Release(); Object.DestroyImmediate(target); }
            }
        }

        private static void Save(Camera camera, RenderTexture target, string path)
        {
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(pixels); }
        }
    }
}
