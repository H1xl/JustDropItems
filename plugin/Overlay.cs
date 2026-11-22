using H1xl.Shared;
using UnityEngine;

namespace JustDropItems
{
    /// <summary>
    /// The mod's own screen overlay canvas for the amount window and the dragged icon, above all of the game's UI
    /// (the item description too). Nothing may be added to the HUD's root: <c>HUD.Update</c> keeps only its last child active
    /// and turns the rest off, so a window placed there as the last child would switch the whole HUD off.
    /// </summary>
    internal static class Overlay
    {
        private static Canvas canvas;

        /// <summary>The overlay, scaled like the canvas of <paramref name="source"/> so sizes match the game's UI.</summary>
        internal static RectTransform For(Transform source)
        {
            if (!canvas)
            {
                var go = new GameObject("JustDropItemsOverlay", typeof(RectTransform), typeof(Canvas));
                UnityEngine.Object.DontDestroyOnLoad(go);
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32000;
                // TranslucentImage (the game's blur) sends its blend amount in uv1; a new canvas drops it unless asked to keep it.
                canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | channels;
            }
            Canvas src = RootCanvas(source);
            if (src)
            {
                canvas.scaleFactor = src.scaleFactor;
                SetLayer(canvas.transform, src.gameObject.layer);
            }
            return (RectTransform)canvas.transform;
        }

        private static void SetLayer(Transform t, int layer)
        {
            if (t.gameObject.layer != layer) t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        /// <summary>Puts a new object on the overlay, on the overlay's layer.</summary>
        internal static void Attach(Transform child, Transform source)
        {
            RectTransform overlay = For(source);
            child.SetParent(overlay, false);
            SetLayer(child, overlay.gameObject.layer);
        }

        private static AdditionalCanvasShaderChannels channels;

        /// <summary>Extra mesh channels the game's own UI canvases use (copied from the tooltip's canvas).</summary>
        internal static void AddShaderChannels(AdditionalCanvasShaderChannels extra)
        {
            channels |= extra;
            if (canvas) canvas.additionalShaderChannels |= extra;
            if (Log.Verbose) Log.Info("Overlay shader channels: " + (channels | AdditionalCanvasShaderChannels.TexCoord1));
        }

        /// <summary>The overlay draws without a camera.</summary>
        internal static Camera Camera => null;

        private static Canvas RootCanvas(Transform source)
        {
            Canvas c = source ? source.GetComponentInParent<Canvas>() : null;
            return c ? c.rootCanvas : null;
        }

        /// <summary>A point of <paramref name="source"/>'s UI in the overlay's local space.</summary>
        internal static Vector2 ToLocal(RectTransform overlay, Transform source, Vector3 world)
        {
            Canvas src = RootCanvas(source);
            Camera cam = src && src.renderMode != RenderMode.ScreenSpaceOverlay ? src.worldCamera : null;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, null, out Vector2 local);
            return local;
        }
    }
}
