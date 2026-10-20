using TMPro;
using UnityEngine;

namespace RaidCommander.UI
{
    /// <summary>Keeps the black outline and the space between outlined glyphs consistent on screen.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class HealthBarTextOutline : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float outlinePixels = 2f;
        private TMP_Text text;
        private Material originalMaterial;
        private Material instanceMaterial;
        private float previousPixelSize = -1f;
        private float originalCharacterSpacing;

        public float OutlinePixels => outlinePixels;

        private void OnEnable()
        {
            text = GetComponent<TMP_Text>();
            originalCharacterSpacing = text.characterSpacing;
            originalMaterial = text.fontSharedMaterial;
            if (originalMaterial == null) return;
            instanceMaterial = new Material(originalMaterial) { hideFlags = HideFlags.DontSave };
            instanceMaterial.EnableKeyword("OUTLINE_ON");
            text.fontSharedMaterial = instanceMaterial;
            previousPixelSize = -1f;
            Canvas.preWillRenderCanvases += UpdateOutline;
            UpdateOutline();
        }

        private void OnDisable()
        {
            Canvas.preWillRenderCanvases -= UpdateOutline;
            if (text != null && text.fontSharedMaterial == instanceMaterial)
            {
                text.fontSharedMaterial = originalMaterial;
                text.characterSpacing = originalCharacterSpacing;
            }
            if (instanceMaterial != null)
            {
                if (Application.isPlaying) Destroy(instanceMaterial);
                else DestroyImmediate(instanceMaterial);
            }
            instanceMaterial = null;
        }

        private void UpdateOutline()
        {
            if (instanceMaterial == null || text.font == null || text.canvas == null) return;
            Transform rect = text.rectTransform;
            Vector3 bottom = rect.TransformPoint(Vector3.zero);
            Vector3 top = rect.TransformPoint(Vector3.up);
            Camera camera = text.canvas.worldCamera;
            float pixelScale = text.canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? Vector3.Distance(bottom, top)
                : Vector2.Distance(RectTransformUtility.WorldToScreenPoint(camera, bottom),
                    RectTransformUtility.WorldToScreenPoint(camera, top));
            float pixelSize = text.fontSize * pixelScale;
            if (Mathf.Abs(previousPixelSize - pixelSize) < 0.001f) return;
            previousPixelSize = pixelSize;

            // TMP character spacing is in hundredths of an em. Leave room for the
            // outward strokes of both adjacent glyphs, including at small Canvas scales.
            text.characterSpacing = Mathf.Max(originalCharacterSpacing,
                200f * outlinePixels / Mathf.Max(0.001f, pixelSize));

            instanceMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0f);
            instanceMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0f);
            ShaderUtilities.UpdateShaderRatios(instanceMaterial);
            float gradient = instanceMaterial.GetFloat(ShaderUtilities.ID_GradientScale);
            float ratio = instanceMaterial.GetFloat(ShaderUtilities.ID_ScaleRatio_A);
            float atlasToScreen = pixelSize * text.font.faceInfo.scale / Mathf.Max(1f, text.font.faceInfo.pointSize);
            instanceMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            float width = Mathf.Clamp01(outlinePixels / Mathf.Max(0.001f, gradient * ratio * atlasToScreen));
            // TMP centers an outline on the SDF edge. Matching dilation preserves the white face
            // and moves the whole stroke outside it instead of consuming thin letter stems.
            instanceMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, width);
            instanceMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            ShaderUtilities.UpdateShaderRatios(instanceMaterial);
            text.UpdateMeshPadding();
            text.SetMaterialDirty();
        }
    }
}
