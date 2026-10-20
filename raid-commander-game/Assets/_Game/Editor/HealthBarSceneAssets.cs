using RaidCommander.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace RaidCommander.EditorTools
{
    /// <summary>Shared font/material setup for health bar text; TMP resources remain separate.</summary>
    public static class HealthBarSceneAssets
    {
        private const string Folder = "Assets/_Game/Art/UI/HealthBars";

        private static TMP_FontAsset GetFont()
        {
            EnsureFolder(Folder);
            string path = Folder + "/HealthBarFont.asset";
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font != null) return font;

            if (Resources.Load<TMP_Settings>("TMP Settings") == null)
                TMP_PackageResourceImporter.ImportResources(true, false, false);
            Font source = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf");
            if (source == null)
                throw new System.InvalidOperationException("Import TMP Essential Resources before configuring health bars.");
            font = TMP_FontAsset.CreateFontAsset(source, 48, 24, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, false);
            font.name = "HealthBarFont";
            font.TryAddCharacters("Boss0123456789, /%?");
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            AssetDatabase.CreateAsset(font, path);
            font.material.name = "HealthBarFont Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (Texture2D atlas in font.atlasTextures)
            {
                atlas.name = "HealthBarFont Atlas";
                AssetDatabase.AddObjectToAsset(atlas, font);
            }
            EditorUtility.SetDirty(font);
            return font;
        }

        private static Material GetTextMaterial(TMP_FontAsset font)
        {
            string path = Folder + "/HealthBarText.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(font.material) { name = "HealthBarText" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.EnableKeyword("OUTLINE_ON");
            material.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.2f);
            material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            material.SetFloat(ShaderUtilities.ID_OutlineSoftness, 0f);
            ShaderUtilities.UpdateShaderRatios(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static TextMeshProUGUI ConfigureText(Transform parent, string name, string value,
            TextAlignmentOptions alignment)
        {
            Transform existing = parent.Find(name);
            GameObject target = existing != null ? existing.gameObject
                : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            target.transform.SetParent(parent, false);
            var legacy = target.GetComponent<UnityEngine.UI.Text>();
            if (legacy != null) Object.DestroyImmediate(legacy);
            var text = target.GetComponent<TextMeshProUGUI>();
            if (text == null) text = target.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset font = GetFont();
            text.font = font;
            text.fontSharedMaterial = GetTextMaterial(font);
            text.text = value;
            text.fontSize = 20f;
            text.characterSpacing = 20f;
            text.fontStyle = FontStyles.Normal;
            text.enableAutoSizing = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            if (target.GetComponent<HealthBarTextOutline>() == null)
                target.AddComponent<HealthBarTextOutline>();
            EditorUtility.SetDirty(text);
            return text;
        }

        public static void ConfigureFill(UnityEngine.UI.Image fill)
        {
            // Image.Type.Filled requires a sprite; a sprite-less Image always renders a full quad.
            fill.sprite = GetFillSprite();
            fill.type = UnityEngine.UI.Image.Type.Filled;
            fill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
        }

        public static void ConfigurePartyOutline(UnityEngine.UI.Image background,
            RaidCommander.Selection.PartyRole role)
        {
            Color color = role switch
            {
                RaidCommander.Selection.PartyRole.Tank => new Color(0.08f, 0.208f, 0.38f, 1f),
                RaidCommander.Selection.PartyRole.Healer => new Color(0.088f, 0.328f, 0.192f, 1f),
                RaidCommander.Selection.PartyRole.Dps => new Color(0.392f, 0.26f, 0.072f, 1f),
                RaidCommander.Selection.PartyRole.RangedDps => new Color(0f, 0.36f, 0.4f, 1f),
                _ => new Color(0.08f, 0.08f, 0.08f, 1f)
            };
            var outline = background.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = background.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            // The party bar projects 14 Canvas units to 10 screen pixels at every zoom.
            outline.effectDistance = Vector2.one * 1.4f;
            outline.useGraphicAlpha = false;
            EditorUtility.SetDirty(outline);
        }

        private static Sprite GetFillSprite()
        {
            EnsureFolder(Folder);
            string path = Folder + "/HealthBarFill.asset";
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if (asset is Sprite existing) return existing;

            // A solid rectangle has no transparent button corners or texture padding.
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "HealthBarFill", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            AssetDatabase.CreateAsset(texture, path);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f),
                new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "HealthBarFill";
            AssetDatabase.AddObjectToAsset(sprite, texture);
            return sprite;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }
    }
}
