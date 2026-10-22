using System;
using System.Reflection;
using RaidCommander.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RaidCommander.Tests.Editor
{
    /// <summary>Resolution and safe-area checks against the real Play-mode Game view.</summary>
    public static class CombatHudVerification
    {
        private static object group;
        private static EditorWindow window;
        private static PropertyInfo selected;
        private static int originalIndex = -1;
        private static int originalCount;
        private static int temporaryIndex = -1;

        public static void SetResolution(int width, int height)
        {
            if (originalIndex < 0)
            {
                Assembly editor = typeof(EditorWindow).Assembly;
                Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
                object sizes = sizesType.BaseType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                Type groupType = editor.GetType("UnityEditor.GameViewSizeGroupType");
                group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { Enum.Parse(groupType, "Standalone") });
                window = EditorWindow.GetWindow(editor.GetType("UnityEditor.GameView"));
                selected = window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                originalIndex = (int)selected.GetValue(window);
                originalCount = Count();
            }
            if (temporaryIndex >= 0)
            {
                selected.SetValue(window, originalIndex);
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { temporaryIndex });
            }
            Assembly assembly = typeof(EditorWindow).Assembly;
            Type sizeType = assembly.GetType("UnityEditor.GameViewSize");
            Type modeType = assembly.GetType("UnityEditor.GameViewSizeType");
            object size = Activator.CreateInstance(sizeType, new[] {
                Enum.Parse(modeType, "FixedResolution"), (object)width, height, "T9 HUD verification" });
            temporaryIndex = Count();
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            selected.SetValue(window, temporaryIndex);
            window.Repaint();
        }

        public static void RestoreResolution()
        {
            if (originalIndex < 0) return;
            selected.SetValue(window, originalIndex);
            for (int index = Count() - 1; index >= originalCount; index--)
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
            window.Repaint();
            originalIndex = temporaryIndex = -1;
        }

        public static string CheckLayout()
        {
            var hud = UnityEngine.Object.FindFirstObjectByType<CombatHud>();
            if (hud == null) throw new InvalidOperationException("Missing combat HUD.");
            Transform root = hud.transform;
            Rect safe = Rect.MinMaxRect(Screen.width * .25f, Screen.height * .25f,
                Screen.width * .75f, Screen.height * .75f);
            foreach (string name in new[] { "Damage Meters Mock", "Combat Log", "Combat Debug",
                "Party Frames", "Group Abilities Mock" })
            {
                Transform child = root.Find(name);
                if (child == null) throw new InvalidOperationException("Missing panel " + name);
                Rect bounds = Bounds((RectTransform)child);
                if (bounds.Overlaps(safe)) throw new InvalidOperationException(name + " covers the central safe rectangle.");
                if (bounds.xMin < -1 || bounds.yMin < -1 || bounds.xMax > Screen.width + 1
                    || bounds.yMax > Screen.height + 1)
                    throw new InvalidOperationException(name + " extends off-screen.");
            }
            var boss = UnityEngine.Object.FindFirstObjectByType<ScreenBossHealthBar>();
            if (boss == null || Bounds((RectTransform)boss.transform.Find("Boss Health Bar")).Overlaps(safe))
                throw new InvalidOperationException("Boss HUD covers the central safe rectangle.");
            Transform party = root.Find("Party Frames");
            for (int i = 1; i <= 4; i++)
            {
                Transform frame = party.Find("Frame " + i);
                if (frame == null) throw new InvalidOperationException("Missing party frame " + i);
                var binding = frame.GetComponent<HealthBarBinding>();
                if (binding == null || binding.Unit == null || binding.FillImage == null
                    || binding.HealthValueLabel == null)
                    throw new InvalidOperationException("Unbound party frame " + i);
                TMP_Text label = frame.Find("Body").GetComponentInChildren<TMP_Text>();
                float scale = hud.GetComponent<Canvas>().scaleFactor;
                if (label.fontSize * scale < 14f || binding.HealthValueLabel.fontSize * scale < 14f)
                    throw new InvalidOperationException("Party text is below 14 rendered pixels.");
                if (binding.HealthValueLabel.preferredWidth > binding.HealthValueLabel.rectTransform.rect.width + 1f)
                    throw new InvalidOperationException("HP text clips in frame " + i);
            }
            return Screen.width + "x" + Screen.height + " HUD layout pass";
        }

        private static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        private static int Count() => (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
    }
}
