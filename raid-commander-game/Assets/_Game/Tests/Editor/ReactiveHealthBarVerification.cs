using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Reflection;
using RaidCommander.CameraControl;
using RaidCommander.Combat;
using RaidCommander.Movement;
using RaidCommander.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace RaidCommander.Verification
{
    /// <summary>Play-mode integration checks using real combat actions, UI meshes, navigation and Game views.</summary>
    public static class ReactiveHealthBarVerification
    {
        [MenuItem("Raid Commander/Verify Reactive Health Bars in Play Mode")]
        public static void Begin() => CombatVerification.Begin();

        public static IEnumerator Verify()
        {
            CombatUnit tank = GameObject.Find("Tank").GetComponent<CombatUnit>();
            CombatUnit healer = GameObject.Find("Healer").GetComponent<CombatUnit>();
            CombatUnit dps = GameObject.Find("DPS").GetComponent<CombatUnit>();
            CombatUnit boss = GameObject.Find("Raid Boss").GetComponent<CombatUnit>();
            CombatUnit rangedDps = GameObject.Find("Ranged DPS").GetComponent<CombatUnit>();
            CombatUnit[] units = { tank, healer, dps, rangedDps, boss };
            var combat = UnityEngine.Object.FindAnyObjectByType<CombatSystem>();
            var actions = UnityEngine.Object.FindAnyObjectByType<CombatDebugActions>();
            HealthBarBinding tankBar = tank.GetComponentInChildren<HealthBarBinding>();
            HealthBarBinding healerBar = healer.GetComponentInChildren<HealthBarBinding>();
            HealthBarBinding dpsBar = dps.GetComponentInChildren<HealthBarBinding>();
            HealthBarBinding rangedBar = rangedDps.GetComponentInChildren<HealthBarBinding>();
            HealthBarBinding bossBar = GameObject.Find("Boss Status HUD").GetComponent<HealthBarBinding>();
            Camera camera = Camera.main;
            var cameraController = camera.GetComponent<OrthographicArenaCamera>();
            bool cameraEnabled = cameraController.enabled;
            float zoom = camera.orthographicSize;
            Vector3 cameraPosition = camera.transform.position;
            Vector3 bossPosition = boss.transform.position;
            Vector3 tankPosition = tank.transform.position;
            int tankMaximum = tank.MaximumHealth;
            int bossMaximum = boss.MaximumHealth;
            GameObject temporary = null;

            try
            {
                actions.ResetEncounter();
                foreach (HealthBarBinding bar in new[] { tankBar, healerBar, dpsBar, rangedBar, bossBar }) AssertView(bar);
                Check(boss.GetComponentInChildren<WorldHealthBar>(true) == null, "Boss has a duplicate world-space bar.");
                Check(bossBar.GetComponent<Canvas>().renderMode == RenderMode.ScreenSpaceOverlay
                    && bossBar.transform.parent == null, "Boss HUD must be an independent overlay.");

                actions.DamageTank();
                AssertView(tankBar);
                Check(tankBar.FillImage.fillAmount < 1f && healerBar.FillImage.fillAmount == 1f
                    && dpsBar.FillImage.fillAmount == 1f && bossBar.FillImage.fillAmount == 1f,
                    "One unit's damage changed another bar or did not decrease its own bar.");
                actions.HealerHeal();
                AssertView(tankBar);
                combat.ApplyHealing(healer, tank, tank.MaximumHealth);
                AssertView(tankBar);
                Check(tankBar.FillImage.fillAmount == 1f, "Healing to maximum did not fill the bar.");

                foreach (HealthBarBinding bar in new[] { tankBar, bossBar })
                {
                    Check(SubscriberCount(bar.Unit, bar) == 1, "Expected one health subscription per enabled bar.");
                    bar.enabled = false;
                    Check(SubscriberCount(bar.Unit, bar) == 0, "Disabled bar retained its health subscription.");
                    float previousFill = bar.FillImage.fillAmount;
                    combat.ApplyDamage(null, bar.Unit, 7);
                    Check(bar.FillImage.fillAmount == previousFill, "Disabled binding still received a health event.");
                    bar.enabled = true;
                    AssertView(bar);
                    bar.gameObject.SetActive(false);
                    combat.ApplyDamage(null, bar.Unit, 7);
                    bar.gameObject.SetActive(true);
                    AssertView(bar);
                    Check(SubscriberCount(bar.Unit, bar) == 1, "Re-enabling duplicated the subscription.");
                }

                tankBar.Bind(healer);
                Check(SubscriberCount(tank, tankBar) == 0 && SubscriberCount(healer, tankBar) == 1,
                    "Rebinding did not release the old unit.");
                combat.ApplyDamage(null, healer, 11);
                AssertView(tankBar);
                tankBar.Bind(tank);
                temporary = new GameObject("Temporary Health Binding", typeof(HealthBarBinding));
                HealthBarBinding temporaryBinding = temporary.GetComponent<HealthBarBinding>();
                temporaryBinding.Bind(tank);
                UnityEngine.Object.Destroy(temporary);
                int frame = Time.frameCount;
                while (Time.frameCount < frame + 2) yield return null;
                Check(SubscriberCount(tank, temporaryBinding) == 0, "Destroyed bar retained its event subscription.");

                int notifications = 0;
                Action<CombatUnit> changed = unit =>
                {
                    notifications++;
                    Check(unit.CurrentHealth <= unit.MaximumHealth, "Health event preceded state application.");
                };
                tank.HealthChanged += changed;
                try
                {
                    tank.SetMaximumHealth(50);
                    Check(tank.CurrentHealth == 50 && notifications == 1, "A lower cap did not clamp and notify once.");
                    AssertView(tankBar);
                    tank.SetMaximumHealth(200);
                    Check(tank.CurrentHealth == 50 && notifications == 2, "Increasing maximum health healed the unit.");
                    AssertView(tankBar);
                    tank.SetMaximumHealth(200);
                    Check(notifications == 2, "An unchanged cap published a health-change event.");
                    tank.SetMaximumHealth(0);
                    AssertView(tankBar);
                    Check(!tank.IsAlive, "A zero maximum must leave an empty, inactive combat unit.");
                    actions.ResetEncounter();
                    Check(!tank.GetComponent<UnitMovement>().enabled,
                        "Resetting a zero-health unit re-enabled its movement executor.");
                    AssertView(tankBar);
                    tank.SetMaximumHealth(tankMaximum);
                    Check(!tank.IsAlive, "Changing a defeated unit's cap revived it.");
                }
                finally { tank.HealthChanged -= changed; }
                actions.ResetEncounter();

                boss.SetMaximumHealth(10000);
                actions.ResetEncounter();
                combat.ApplyDamage(tank, boss, 2500);
                AssertView(bossBar);
                Check(bossBar.PercentageLabel.text == "75%" && bossBar.HealthValueLabel.text == "7,500 / 10,000",
                    "Boss percentage or thousands formatting is incorrect.");
                combat.ApplyHealing(null, boss, 10000);
                AssertView(bossBar);
                actions.KillBoss();
                AssertView(bossBar);
                Check(bossBar.PercentageLabel.text == "0%" && bossBar.HealthValueLabel.text == "0 / 10,000",
                    "Boss death did not empty its HUD and text together.");
                actions.ResetEncounter();
                AssertView(bossBar);
                boss.SetMaximumHealth(0);
                AssertView(bossBar);
                Check(bossBar.PercentageLabel.text == "0%" && bossBar.HealthValueLabel.text == "0 / 0",
                    "Zero maximum health is unsafe in the boss text.");
                boss.SetMaximumHealth(10000);
                actions.ResetEncounter();

                var logSettings = new SerializedObject(combat);
                var log = combat.LogService;
                logSettings.FindProperty("logService").objectReferenceValue = null;
                logSettings.ApplyModifiedPropertiesWithoutUndo();
                try
                {
                    actions.DamageTank();
                    AssertView(tankBar);
                }
                finally
                {
                    logSettings.FindProperty("logService").objectReferenceValue = log;
                    logSettings.ApplyModifiedPropertiesWithoutUndo();
                }
                Check(HealthBarBinding.Fraction(10, 0) == 0f && HealthBarBinding.Fraction(150, 100) == 1f
                    && HealthBarBinding.Fraction(-10, 100) == 0f, "Fill fraction is unsafe or not clamped.");

                cameraController.enabled = false;
                var worldBar = tank.GetComponentInChildren<WorldHealthBar>();
                foreach (float size in new[] { 8f, 30f })
                {
                    camera.orthographicSize = size;
                    frame = Time.frameCount;
                    while (Time.frameCount < frame + 2) yield return null;
                    float height = ProjectedHeight((RectTransform)worldBar.transform, camera);
                    Check(height >= 8f && height <= 12f, "Party bar lost its readable screen size at zoom " + size + ": " + height);
                }

                Rect hudBefore = ScreenRect(bossBar.FillImage.rectTransform.parent as RectTransform);
                camera.transform.position += new Vector3(3f, 2f, 1f);
                boss.transform.position += Vector3.right * 3f;
                frame = Time.frameCount;
                while (Time.frameCount < frame + 2) yield return null;
                Check(ScreenRect(bossBar.FillImage.rectTransform.parent as RectTransform) == hudBefore,
                    "Boss HUD followed the boss or camera.");

                var movement = tank.GetComponent<UnitMovement>();
                Check(UnityEngine.AI.NavMesh.SamplePosition(tankPosition + Vector3.right * 2f,
                    out UnityEngine.AI.NavMeshHit floor, 2f, UnityEngine.AI.NavMesh.AllAreas),
                    "No ground destination for the health bar follow check.");
                MoveResult moveResult = movement.TryMove(floor.position, MovementSource.Manual);
                Check(moveResult == MoveResult.Accepted, "Could not issue party movement for the follow check: " + moveResult);
                double deadline = EditorApplication.timeSinceStartup + 5;
                while (movement.State == MovementState.Moving && EditorApplication.timeSinceStartup < deadline) yield return null;
                frame = Time.frameCount;
                while (Time.frameCount < frame + 2) yield return null;
                Check(Vector3.Distance(tankPosition, tank.transform.position) > 1f, "Party unit did not move during verification.");
                Check(Vector3.Distance(worldBar.transform.position, tank.transform.position + Vector3.up * 1.8f) < 0.02f,
                    "World health bar did not follow its character with the configured offset.");

                using (var gameView = new GameViewSizesScope())
                {
                    foreach (Vector2Int size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080),
                        new Vector2Int(1024, 768), new Vector2Int(720, 1280), new Vector2Int(640, 480) })
                    {
                        gameView.Set(size.x, size.y);
                        frame = Time.frameCount;
                        while (Time.frameCount < frame + 3) yield return null;
                        Check(Screen.width == size.x && Screen.height == size.y, "Game view did not adopt " + size);
                        AssertHudLayout(bossBar);
                        AssertView(bossBar);
                    }
                }
                Debug.Log("HEALTH BAR VERIFY PASS: events, meshes, text, subscriptions, maximum health, movement, zoom and five resolutions.");
            }
            finally
            {
                if (temporary != null) UnityEngine.Object.Destroy(temporary);
                camera.orthographicSize = zoom;
                camera.transform.position = cameraPosition;
                cameraController.enabled = cameraEnabled;
                tank.GetComponent<UnitMovement>().Cancel();
                var agent = tank.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent.isOnNavMesh) agent.Warp(tankPosition);
                boss.transform.position = bossPosition;
                tank.SetMaximumHealth(tankMaximum);
                boss.SetMaximumHealth(bossMaximum);
                combat.ResetSession(units);
            }
        }

        private static void AssertView(HealthBarBinding bar)
        {
            Check(bar != null && bar.Unit != null && bar.FillImage != null, "Unconfigured health binding.");
            float fraction = HealthBarBinding.Fraction(bar.Unit.CurrentHealth, bar.Unit.MaximumHealth);
            Check(Mathf.Abs(bar.FillImage.fillAmount - fraction) < 0.0001f, "Health fill is stale for " + bar.Unit.name);
            if (bar.PercentageLabel != null)
                Check(bar.PercentageLabel.text == Mathf.RoundToInt(fraction * 100f) + "%", "Percentage is stale.");
            if (bar.HealthValueLabel != null)
                Check(bar.HealthValueLabel.text == bar.Unit.CurrentHealth.ToString("N0", CultureInfo.InvariantCulture)
                    + " / " + bar.Unit.MaximumHealth.ToString("N0", CultureInfo.InvariantCulture), "Health values are stale.");
            Canvas.ForceUpdateCanvases();
            Mesh mesh = bar.FillImage.canvasRenderer.GetMesh();
            float renderedWidth = mesh != null && mesh.vertexCount > 0 ? mesh.bounds.size.x : 0f;
            Check(bar.FillImage.sprite != null, "A sprite-less Image cannot render partial health.");
            Vector4 padding = UnityEngine.Sprites.DataUtility.GetPadding(bar.FillImage.sprite);
            float spriteWidth = Mathf.Round(bar.FillImage.sprite.rect.width);
            float drawableWidth = bar.FillImage.rectTransform.rect.width * (1f - (padding.x + padding.z) / spriteWidth);
            Check(Mathf.Abs(renderedWidth - drawableWidth * fraction) < 0.1f,
                "Rendered fill geometry does not match health for " + bar.Unit.name + ": " + renderedWidth);
        }

        private static void AssertHudLayout(HealthBarBinding bar)
        {
            Canvas.ForceUpdateCanvases();
            Rect rect = ScreenRect(bar.FillImage.rectTransform.parent as RectTransform);
            Check(rect.xMin >= 0 && rect.xMax <= Screen.width && rect.yMax <= Screen.height
                && Screen.height - rect.yMax < 30, "Boss HUD fell outside its top screen layout.");
            Check(ScreenRect(bar.PercentageLabel.rectTransform).center.x < rect.center.x
                && ScreenRect(bar.HealthValueLabel.rectTransform).center.x > rect.center.x,
                "Boss numerical values are not on their specified sides.");
            foreach (TMP_Text text in bar.GetComponentsInChildren<TMP_Text>())
            {
                Check(text.GetComponent<HealthBarTextOutline>().OutlinePixels == 2f
                    && text.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth) > 0f
                    && text.fontSharedMaterial.GetColor(ShaderUtilities.ID_OutlineColor) == Color.black,
                    "Missing visible black two-pixel outline on " + text.name);
                Check(text.fontSize * bar.GetComponent<Canvas>().scaleFactor >= 13.9f, "HUD text became too small.");
                Check(text.preferredWidth <= text.rectTransform.rect.width + 1f, "HUD text overflows its layout: " + text.name);
                if (text.name == "Boss Name")
                    Check(ScreenRect(text.rectTransform).yMax < rect.yMin, "Boss name is not below the bar.");
            }
        }

        private static int SubscriberCount(CombatUnit unit, HealthBarBinding binding)
        {
            var handlers = typeof(CombatUnit).GetField("HealthChanged", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(unit) as Delegate;
            return handlers?.GetInvocationList().Count(handler => ReferenceEquals(handler.Target, binding)) ?? 0;
        }

        private static Rect ScreenRect(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static float ProjectedHeight(RectTransform rect, Camera camera)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Vector2.Distance(camera.WorldToScreenPoint(corners[0]), camera.WorldToScreenPoint(corners[1]));
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private sealed class GameViewSizesScope : IDisposable
        {
            private readonly object group;
            private readonly EditorWindow window;
            private readonly PropertyInfo selected;
            private readonly int originalIndex;
            private readonly int originalCount;

            public GameViewSizesScope()
            {
                Assembly editor = typeof(EditorWindow).Assembly;
                Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
                object sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                Type groupType = editor.GetType("UnityEditor.GameViewSizeGroupType");
                group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
                window = EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView"));
                selected = window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                // Remove only this verifier's own leftovers from an interrupted run.
                int builtinCount = (int)group.GetType().GetMethod("GetBuiltinCount").Invoke(group, null);
                for (int index = Count() - 1; index >= builtinCount; index--)
                {
                    object size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { index });
                    string name = size.GetType().GetProperty("baseText").GetValue(size) as string;
                    if (name == "Health bar verification")
                        group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                }
                originalIndex = (int)selected.GetValue(window);
                originalCount = Count();
            }

            public void Set(int width, int height)
            {
                Assembly editor = typeof(EditorWindow).Assembly;
                Type sizeType = editor.GetType("UnityEditor.GameViewSize");
                Type modeType = editor.GetType("UnityEditor.GameViewSizeType");
                object size = Activator.CreateInstance(sizeType, new[] { Enum.Parse(modeType, "FixedResolution"),
                    (object)width, height, "Health bar verification" });
                int index = Count();
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                selected.SetValue(window, index);
                window.Repaint();
            }

            public void Dispose()
            {
                selected.SetValue(window, originalIndex);
                // Unity 6 expects the global size index, including built-in entries.
                // Keep cleanup bounded even if an Editor API rejects an entry.
                for (int index = Count() - 1; index >= originalCount; index--)
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                window.Repaint();
            }

            private int Count() => (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
        }
    }
}
