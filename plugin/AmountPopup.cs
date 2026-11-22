using System.Text;
using System.Text.RegularExpressions;
using H1xl.Shared;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace JustDropItems
{
    /// <summary>
    /// The amount window beside a hovered icon: a bar split into Permanent, Ethereal, Temporary and Shared copies with the chosen
    /// amount filled in, the amount to the right of the bar and the split under it. Kinds the host doesn't allow are crossed out.
    /// The background is a copy of the panel behind the item description (the game's tooltip, which blurs the world behind it)
    /// with the frame of the HUD's panels on top.
    /// It never takes the mouse, so the icon under it stays hovered.
    /// </summary>
    internal static class AmountPopup
    {
        private const float BarWidth = 200f, Pad = 10f, BarHeight = 14f, Gap = 10f, NumberSize = 20f, BreakdownSize = 15f,
                            RowHeight = 24f, IconGap = 8f, CrossThickness = 2f;

        private static GameObject root;
        private static RectTransform rootRt, barRt;
        private static Image background, frame, loadoutPanel;
        private static Color loadoutPanelColor;
        private static CanvasGroup panelGroup; // the copied game panel, if there is one
        private static readonly Image[] dim = new Image[4], fill = new Image[4];
        private static readonly Image[] ticks = new Image[3];
        private static readonly RectTransform[] crosses = new RectTransform[4];
        private static TextMeshProUGUI numbers, breakdown;
        private static object owner;

        private static bool styleLoaded;
        private static GameObject tooltipPanel; // the description's panel in the game's tooltip prefab
        // Fallback when the tooltip panel isn't found: a plain dark panel with a thin frame.
        private static readonly Color panelColor = new Color(0.10f, 0.10f, 0.12f);
        private static readonly Color frameColor = new Color(0.55f, 0.55f, 0.6f);
        private static TMP_FontAsset font;
        private static Material fontMaterial;
        private static readonly Color crossColor = new Color(1f, 0.38f, 0.38f, 0.95f);
        private static readonly Color[] kindColors =
        {
            new Color(0.92f, 0.92f, 0.92f),  // Permanent
            new Color(0.62f, 0.85f, 1f),     // Ethereal (replaced by the game's cIsRental colour when found)
            new Color(0.85f, 0.75f, 0.55f),  // Temporary (replaced by cIsTemporary)
            new Color(0.45f, 0.45f, 0.45f),  // Shared: can't move
        };

        internal static void Hide(object who)
        {
            if (root && owner == who) root.SetActive(false);
        }

        // What the open window shows; it is rebuilt only when something here changes (callers redraw it every frame).
        private static RectTransform shownIcon;
        private static bool shownBar;
        private static StackModel shownModel;
        private static int shownAmount, shownMax;
        private static string shownText;
        private static float shownScale, shownOpacity;
        private static PopupSide shownSide;
        private static bool shownBreakdown;
        private static Language shownLanguage;

        /// <summary>True if the open window already shows this; otherwise remembers it as the new content.</summary>
        private static bool Unchanged(object who, RectTransform icon, bool bar, StackModel m, int amount, int max, string text)
        {
            StackModel s = shownModel;
            bool same = root && root.activeSelf && owner == who && shownIcon == icon && shownBar == bar &&
                        s.permanent == m.permanent && s.ethereal == m.ethereal && s.temporary == m.temporary && s.shared == m.shared &&
                        s.etherealAllowed == m.etherealAllowed && s.temporaryAllowed == m.temporaryAllowed && s.order == m.order &&
                        shownAmount == amount && shownMax == max && string.Equals(shownText, text) &&
                        shownScale == Settings.popupScale.Value && shownOpacity == Settings.popupOpacity.Value &&
                        shownSide == Settings.popupSide.Value && shownBreakdown == Settings.showBreakdown.Value &&
                        shownLanguage == Language.currentLanguage;
            if (same) return true;
            shownIcon = icon;
            shownBar = bar;
            shownModel = m;
            shownAmount = amount;
            shownMax = max;
            shownText = text;
            shownScale = Settings.popupScale.Value;
            shownOpacity = Settings.popupOpacity.Value;
            shownSide = Settings.popupSide.Value;
            shownBreakdown = Settings.showBreakdown.Value;
            shownLanguage = Language.currentLanguage;
            return false;
        }

        /// <summary>Same content: only follows the icon, which may move (the scoreboard's layout).</summary>
        private static void Follow(RectTransform icon) =>
            Position(icon, (RectTransform)rootRt.parent, rootRt.sizeDelta * Settings.popupScale.Value);

        /// <summary>Opens the window without the bar: only <paramref name="text"/> (equipment, the gamepad's recipient choice).</summary>
        internal static void ShowText(object who, RectTransform icon, string text)
        {
            if (!icon) return;
            if (Unchanged(who, icon, false, default, 0, 0, text))
            {
                Follow(icon);
                return;
            }
            RectTransform overlay = Open(who, icon, withBar: false);
            float width = 2 * Pad + 60f, height = 2 * Pad;
            PlaceText(text, -Pad, ref width, ref height);
            Finish(icon, overlay, width, height);
        }

        private static RectTransform Open(object who, RectTransform icon, bool withBar)
        {
            RectTransform overlay = Overlay.For(icon);
            Build(overlay);
            owner = who;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            ApplyBackground();
            barRt.gameObject.SetActive(withBar);
            numbers.gameObject.SetActive(withBar);
            float scale = Settings.popupScale.Value;
            rootRt.localScale = new Vector3(scale, scale, 1f);
            return overlay;
        }

        /// <summary>The text under the bar (or alone), measured so the window grows to fit it.</summary>
        private static void PlaceText(string text, float top, ref float width, ref float height)
        {
            breakdown.gameObject.SetActive(!string.IsNullOrEmpty(text));
            if (string.IsNullOrEmpty(text)) return;
            breakdown.text = text;
            Vector2 size = breakdown.GetPreferredValues(text);
            width = Mathf.Max(width, Mathf.Ceil(size.x) + 2f + 2 * Pad);
            var brt = breakdown.rectTransform;
            brt.anchoredPosition = new Vector2(0f, top);
            brt.sizeDelta = new Vector2(-2 * Pad, Mathf.Ceil(size.y) + 2f);
            height += Mathf.Ceil(size.y) + 4f;
        }

        private static void Finish(RectTransform icon, RectTransform overlay, float width, float height)
        {
            rootRt.sizeDelta = new Vector2(width, height);
            Position(icon, overlay, new Vector2(width, height) * Settings.popupScale.Value);
        }

        /// <param name="extra">More lines under the bar (the gamepad's button hints).</param>
        internal static void Show(object who, RectTransform icon, StackModel model, int amount, int max, string extra = null)
        {
            if (!icon) return;
            if (Unchanged(who, icon, true, model, amount, max, extra))
            {
                Follow(icon);
                return;
            }
            RectTransform overlay = Open(who, icon, withBar: true);

            // Bar: every copy of the stack, each kind in its colour; the chosen amount is bright, the rest dim.
            // A kind the host doesn't allow (and Shared copies, which never move) is crossed out.
            // A stack bigger than one action may move shows only the copies that can be taken, so the amount stays visible.
            model.Split(amount, out int p, out int e, out int t);
            bool capped = model.Movable > max;
            int[] counts;
            if (capped)
            {
                model.Split(max, out int mp, out int me, out int mt);
                counts = new[] { mp, me, mt, 0 };
            }
            else counts = new[] { model.permanent, model.ethereal, model.temporary, model.shared };
            int[] chosen = { p, e, t, 0 };
            int total = 0;
            foreach (int c in counts) total += c;
            total = Mathf.Max(1, total);
            float x = 0f;
            int tick = 0;
            // The movable kinds in the player's order (BarOrder), then the Shared copies, always last.
            ItemKind[] kinds = model.Kinds();
            for (int pos = 0; pos < 4; pos++)
            {
                int k = pos < 3 ? (int)kinds[pos] : (int)ItemKind.Shared;
                float w = BarWidth * counts[k] / total;
                Place(dim[k].rectTransform, x, w);
                dim[k].enabled = w > 0.01f;
                float fw = BarWidth * chosen[k] / total;
                Place(fill[k].rectTransform, x, fw);
                fill[k].enabled = fw > 0.01f;
                Color c = kindColors[k];
                bool movable = model.Allowed((ItemKind)k);
                dim[k].color = new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, movable ? 1f : 0.6f);
                fill[k].color = c;
                Cross(crosses[k], x, w, counts[k] > 0 && !movable);
                x += w;
                if (counts[k] > 0 && pos < 3 && tick < ticks.Length && x < BarWidth - 0.5f)
                {
                    ticks[tick].gameObject.SetActive(true);
                    Place(ticks[tick].rectTransform, x - 1f, 2f);
                    tick++;
                }
            }
            for (; tick < ticks.Length; tick++) ticks[tick].gameObject.SetActive(false);

            // The amount, right of the bar. Its width is measured for the widest amount, so the window doesn't jump while scrolling.
            string ofStack = capped ? " <size=70%><color=#8C8C8C>" + Language.GetStringFormatted(Tokens.OfStack, model.Total) + "</color></size>" : string.Empty;
            numbers.text = $"{amount} / {max}{ofStack}";
            float numberWidth = Mathf.Ceil(numbers.GetPreferredValues($"{max} / {max}{ofStack}").x) + 2f;
            var nrt = numbers.rectTransform;
            nrt.anchoredPosition = new Vector2(Pad + BarWidth + Gap, -Pad);
            nrt.sizeDelta = new Vector2(numberWidth, RowHeight);
            float width = Pad + BarWidth + Gap + numberWidth + Pad;

            bool showBreakdown = Settings.showBreakdown.Value && (model.ethereal > 0 || model.temporary > 0);
            string text = showBreakdown ? Breakdown(model.Kinds(), p, e, t) : null;
            if (!string.IsNullOrEmpty(extra)) text = text == null ? extra : text + "\n" + extra;
            float height = Pad + RowHeight + Pad;
            PlaceText(text, -(Pad + RowHeight + 2f), ref width, ref height);
            Finish(icon, overlay, width, height);
        }

        private static string Breakdown(ItemKind[] kinds, int p, int e, int t)
        {
            var parts = new System.Collections.Generic.List<string>(3);
            foreach (ItemKind k in kinds)
            {
                int n = k == ItemKind.Permanent ? p : k == ItemKind.Ethereal ? e : t;
                if (n > 0) parts.Add(n + " " + Tokens.KindName(k));
            }
            return string.Join("  ·  ", parts);
        }

        private static void Place(RectTransform rt, float x, float width)
        {
            rt.anchoredPosition = new Vector2(x, 0f);
            rt.sizeDelta = new Vector2(Mathf.Max(0f, width), 0f);
        }

        /// <summary>Two diagonals from corner to corner of a bar segment.</summary>
        private static void Cross(RectTransform cross, float x, float width, bool show)
        {
            show &= width > 0.5f;
            cross.gameObject.SetActive(show);
            if (!show) return;
            Place(cross, x, width);
            float length = Mathf.Sqrt(width * width + BarHeight * BarHeight);
            float angle = Mathf.Atan2(BarHeight, width) * Mathf.Rad2Deg;
            for (int i = 0; i < 2; i++)
            {
                var line = (RectTransform)cross.GetChild(i);
                line.sizeDelta = new Vector2(length, CrossThickness);
                line.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? angle : -angle);
            }
        }

        /// <summary>
        /// The panel with the configured opacity. (The game can't blur what is behind a UI panel: its UI blur shader only blurs
        /// a texture it is given.)
        /// </summary>
        private static void ApplyBackground()
        {
            float a = Settings.popupOpacity.Value;
            if (loadoutPanel)
            {
                Color c = loadoutPanelColor;
                c.a *= a;
                loadoutPanel.color = c;
            }
            if (panelGroup)
            {
                panelGroup.alpha = a;
                return;
            }
            background.color = new Color(panelColor.r, panelColor.g, panelColor.b, a);
            frame.color = new Color(frameColor.r, frameColor.g, frameColor.b, Mathf.Min(1f, a * 0.6f));
        }

        private static readonly Vector3[] corners = new Vector3[4];
        private static readonly PopupSide[] order = { PopupSide.Right, PopupSide.Below, PopupSide.Left, PopupSide.Above };

        /// <summary>Opens on the configured side of the icon; if the window doesn't fit there, tries the next side clockwise.</summary>
        private static void Position(RectTransform icon, RectTransform canvas, Vector2 size)
        {
            icon.GetWorldCorners(corners);
            Vector2 min = Overlay.ToLocal(canvas, icon, corners[0]), max = Overlay.ToLocal(canvas, icon, corners[2]);
            Rect area = canvas.rect;
            int start = Mathf.Max(0, System.Array.IndexOf(order, Settings.popupSide.Value));
            Vector2 mid = (min + max) / 2f;
            for (int i = 0; i < 4; i++)
            {
                Rect rect = SideRect(order[(start + i) % 4], min, max, mid, size);
                if (rect.xMin >= area.xMin && rect.xMax <= area.xMax && rect.yMin >= area.yMin && rect.yMax <= area.yMax)
                {
                    PlaceRect(rect);
                    return;
                }
            }
            // Fits nowhere (a very large scale): the chosen side, pushed inside the screen.
            Rect r = SideRect(order[start], min, max, mid, size);
            r.x = Mathf.Clamp(r.x, area.xMin, Mathf.Max(area.xMin, area.xMax - size.x));
            r.y = Mathf.Clamp(r.y, area.yMin, Mathf.Max(area.yMin, area.yMax - size.y));
            PlaceRect(r);
        }

        private static Rect SideRect(PopupSide side, Vector2 min, Vector2 max, Vector2 mid, Vector2 size)
        {
            switch (side)
            {
                case PopupSide.Right: return new Rect(max.x + IconGap, mid.y - size.y / 2f, size.x, size.y);
                case PopupSide.Below: return new Rect(mid.x - size.x / 2f, min.y - IconGap - size.y, size.x, size.y);
                case PopupSide.Left: return new Rect(min.x - IconGap - size.x, mid.y - size.y / 2f, size.x, size.y);
                default: return new Rect(mid.x - size.x / 2f, max.y + IconGap, size.x, size.y);
            }
        }

        /// <summary>Puts the window at a rectangle given in the canvas' local space (bottom-left pivot).</summary>
        private static void PlaceRect(Rect rect)
        {
            rootRt.pivot = Vector2.zero;
            rootRt.localPosition = new Vector3(rect.x, rect.y, 0f);
        }

        // ---------------------------------------------------------------- building

        private static void LoadStyle()
        {
            if (styleLoaded) return;
            styleLoaded = true;
            GameObject tooltip = LegacyResourcesAPI.Load<GameObject>("Prefabs/UI/Tooltip");
            if (tooltip)
            {
                if (Log.Verbose) Log.Info("Tooltip prefab:\n" + Dump(tooltip.transform, 0));
                Canvas tooltipCanvas = tooltip.GetComponent<Canvas>();
                if (tooltipCanvas) Overlay.AddShaderChannels(tooltipCanvas.additionalShaderChannels);
                tooltipPanel = FindDescriptionPanel(tooltip);
                TextMeshProUGUI text = tooltip.GetComponentInChildren<TextMeshProUGUI>(true);
                if (text)
                {
                    font = text.font;
                    fontMaterial = text.fontSharedMaterial;
                }
            }
            kindColors[1] = StyleColor("cIsRental", kindColors[1]);
            kindColors[2] = StyleColor("cIsTemporary", kindColors[2]);
        }

        /// <summary>The colour of one of the game's text styles, e.g. "cIsTemporary".</summary>
        private static Color StyleColor(string style, Color fallback)
        {
            try
            {
                TMP_Style s = TMP_Settings.defaultStyleSheet ? TMP_Settings.defaultStyleSheet.GetStyle(style) : null;
                Match m = s != null ? Regex.Match(s.styleOpeningDefinition, "#([0-9A-Fa-f]{6,8})") : Match.Empty;
                if (m.Success && ColorUtility.TryParseHtmlString("#" + m.Groups[1].Value, out Color c)) return c;
            }
            catch (System.Exception e)
            {
                Log.Debug($"Style {style}: {e.Message}");
            }
            return fallback;
        }

        private static void Build(Transform canvas)
        {
            if (root)
            {
                if (root.transform.parent != canvas) root.transform.SetParent(canvas, false);
                return;
            }
            LoadStyle();
            root = new GameObject("JustDropItemsAmount", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(canvas, false);
            root.layer = canvas.gameObject.layer;
            var group = root.GetComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            rootRt = (RectTransform)root.transform;
            rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);

            if (!tooltipPanel || !CopyPanel()) BuildPlainPanel();

            // The bar sits on the left of the first row, centred on the amount's line.
            barRt = NewRect("Bar", rootRt);
            barRt.anchorMin = barRt.anchorMax = new Vector2(0f, 1f);
            barRt.pivot = new Vector2(0f, 1f);
            barRt.anchoredPosition = new Vector2(Pad, -(Pad + (RowHeight - BarHeight) / 2f));
            barRt.sizeDelta = new Vector2(BarWidth, BarHeight);
            Image barBack = NewImage("Back", barRt);
            barBack.color = new Color(0f, 0f, 0f, 0.6f);
            Stretch(barBack.rectTransform);
            for (int k = 0; k < 4; k++) dim[k] = Segment("Kind" + k);
            for (int k = 0; k < 4; k++) fill[k] = Segment("Chosen" + k);
            for (int i = 0; i < ticks.Length; i++)
            {
                ticks[i] = Segment("Border" + i);
                ticks[i].color = new Color(1f, 1f, 1f, 0.9f);
            }
            for (int k = 0; k < 4; k++)
            {
                crosses[k] = SegmentRect("Forbidden" + k);
                for (int i = 0; i < 2; i++)
                {
                    Image line = NewImage("Line" + i, crosses[k]);
                    line.color = crossColor;
                    var lrt = line.rectTransform;
                    lrt.anchorMin = lrt.anchorMax = lrt.pivot = new Vector2(0.5f, 0.5f);
                    lrt.anchoredPosition = Vector2.zero;
                }
                crosses[k].gameObject.SetActive(false);
            }

            numbers = NewText("Numbers", NumberSize, FontStyles.Bold);
            var nrt = numbers.rectTransform;
            nrt.anchorMin = nrt.anchorMax = new Vector2(0f, 1f);
            nrt.pivot = new Vector2(0f, 1f);
            numbers.alignment = TextAlignmentOptions.MidlineLeft;
            numbers.enableWordWrapping = false;

            breakdown = NewText("Breakdown", BreakdownSize, FontStyles.Normal);
            var brt = breakdown.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f);
            brt.anchorMax = new Vector2(1f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            breakdown.enableWordWrapping = false;
            AddHudFrame();
            root.SetActive(false);
        }

        /// <summary>
        /// The frame of the HUD's panels (money, item bar, stage: thin lines with thicker corners), taken from the panel around
        /// the HUD's item bar in the HUD prefab: the first image with a sliced sprite on the way up from the item display.
        /// </summary>
        private static void AddHudFrame()
        {
            try
            {
                GameObject prefab = Addressables.LoadAssetAsync<GameObject>("RoR2/Base/UI/HUDSimple.prefab").WaitForCompletion();
                var hud = prefab ? prefab.GetComponentInChildren<RoR2.UI.HUD>(true) : null;
                Transform start = hud && hud.itemInventoryDisplay ? hud.itemInventoryDisplay.transform : null;
                var log = new StringBuilder("HUD frame candidates (item bar and its parents):");
                Image source = null;
                for (Transform t = start; t && t != prefab.transform; t = t.parent)
                {
                    foreach (Image i in t.GetComponents<Image>())
                    {
                        bool frame = i.sprite && i.sprite.border != Vector4.zero && i.sprite.name != "UISprite";
                        log.Append($"\n  {t.name}: sprite {(i.sprite ? i.sprite.name : "none")} border {(i.sprite ? i.sprite.border.ToString() : "-")} " +
                                   $"{i.type}, colour #{ColorUtility.ToHtmlStringRGBA(i.color)}, ppu x{i.pixelsPerUnitMultiplier}, " +
                                   $"material {(i.material && i.material != i.defaultMaterial ? i.material.name : "default")}");
                        if (frame && !source)
                        {
                            source = i;
                            log.Append("  <- used");
                        }
                    }
                    // The frame may also be a child next to the item display (an outline drawn over the panel).
                    for (int c = 0; c < t.childCount && !source; c++)
                    {
                        Image i = t.GetChild(c).GetComponent<Image>();
                        if (!i || !i.sprite || i.sprite.border == Vector4.zero || i.sprite.name == "UISprite") continue;
                        log.Append($"\n  {t.name}/{i.name}: sprite {i.sprite.name} border {i.sprite.border} {i.type}, " +
                                   $"colour #{ColorUtility.ToHtmlStringRGBA(i.color)}  <- used");
                        source = i;
                    }
                    if (source) break;
                }
                if (Log.Verbose) Log.Info(log.ToString());
                if (!source)
                {
                    Log.Warn("HUD frame not found: the amount window has no frame.");
                    return;
                }
                loadoutPanel = NewImage("HudFrame", rootRt);
                loadoutPanel.sprite = source.sprite;
                loadoutPanel.type = source.type == Image.Type.Simple ? Image.Type.Sliced : source.type;
                loadoutPanel.fillCenter = source.fillCenter;
                loadoutPanel.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                if (source.material && source.material != source.defaultMaterial) loadoutPanel.material = source.material;
                loadoutPanelColor = source.color;
                Stretch(loadoutPanel.rectTransform);
                // Above the copied description panel, below the bar and the texts.
                loadoutPanel.transform.SetSiblingIndex(panelGroup ? panelGroup.transform.GetSiblingIndex() + 1 : 0);
            }
            catch (System.Exception e)
            {
                Log.Warn("HUD frame: " + e.Message);
            }
        }

        /// <summary>Removes texts, layout and scripts from a copied piece of the game's UI; its images stay.</summary>
        private static void StripToGraphics(GameObject copy)
        {
            foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
                if (text && text.gameObject != copy) Object.DestroyImmediate(text.gameObject);
            foreach (MonoBehaviour b in copy.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (!b || b is Graphic || b is Mask || b is RectMask2D) continue;
                Object.DestroyImmediate(b);
            }
            foreach (Graphic g in copy.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
        }

        /// <summary>A plain dark panel; its frame is a slightly bigger rectangle behind it, so a 1-pixel edge shows around it.</summary>
        private static void BuildPlainPanel()
        {
            frame = NewImage("Frame", rootRt);
            Stretch(frame.rectTransform);
            frame.rectTransform.offsetMin = new Vector2(-1f, -1f);
            frame.rectTransform.offsetMax = new Vector2(1f, 1f);
            background = NewImage("Background", rootRt);
            Stretch(background.rectTransform);
        }

        /// <summary>
        /// The panel behind the item description: the closest parent of the tooltip's body text that draws an image.
        /// If that parent also holds the title, only its own image is copied (not the title bar).
        /// </summary>
        private static GameObject FindDescriptionPanel(GameObject tooltip)
        {
            var controller = tooltip.GetComponentInChildren<RoR2.UI.TooltipController>(true);
            if (!controller || !controller.bodyLabel) return null;
            for (Transform t = controller.bodyLabel.transform.parent; t && t != tooltip.transform.parent; t = t.parent)
            {
                Image image = t.GetComponent<Image>();
                if (!image || !image.enabled) continue;
                if (Log.Verbose) Log.Info($"Popup panel: copying {t.name}");
                return t.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Puts a copy of the description panel behind the window: its images (frame and all) stay, texts, layout and scripts go.
        /// </summary>
        private static bool CopyPanel()
        {
            var controller = tooltipPanel.GetComponentInParent<RoR2.UI.TooltipController>(true);
            bool holdsTitle = controller && controller.titleLabel && controller.titleLabel.transform.IsChildOf(tooltipPanel.transform);
            GameObject copy;
            if (holdsTitle)
            {
                // Only the panel's own image.
                Image source = tooltipPanel.GetComponent<Image>();
                Image image = NewImage("GamePanel", rootRt);
                image.sprite = source.sprite;
                image.type = source.type;
                image.fillCenter = source.fillCenter;
                image.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
                image.color = source.color;
                if (source.material && source.material != source.defaultMaterial) image.material = source.material;
                copy = image.gameObject;
            }
            else
            {
                copy = Object.Instantiate(tooltipPanel, rootRt, false);
                copy.name = "GamePanel";
                copy.SetActive(true);
                StripToGraphics(copy);
            }
            var rt = (RectTransform)copy.transform;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.pivot = new Vector2(0.5f, 0.5f);
            Stretch(rt);
            copy.transform.SetAsFirstSibling();
            panelGroup = copy.GetComponent<CanvasGroup>() ?? copy.AddComponent<CanvasGroup>();
            panelGroup.blocksRaycasts = false;
            panelGroup.interactable = false;
            return true;
        }

        private static string Dump(Transform t, int depth)
        {
            var sb = new StringBuilder();
            sb.Append(' ', depth * 2).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)");
            foreach (Component c in t.GetComponents<Component>())
            {
                if (!c || c is Transform) continue;
                sb.Append(" [").Append(c.GetType().Name);
                if (c is Image i)
                    sb.Append(' ').Append(i.sprite ? i.sprite.name : "no sprite").Append(' ').Append(i.type).Append(' ')
                      .Append(ColorUtility.ToHtmlStringRGBA(i.color)).Append(i.material && i.material != i.defaultMaterial ? " mat " + i.material.name : "");
                sb.Append(']');
            }
            if (t is RectTransform r) sb.Append($" anchors {r.anchorMin}-{r.anchorMax} offsets {r.offsetMin}/{r.offsetMax}");
            sb.Append('\n');
            for (int k = 0; k < t.childCount; k++) sb.Append(Dump(t.GetChild(k), depth + 1));
            return sb.ToString();
        }

        private static RectTransform SegmentRect(string name)
        {
            RectTransform rt = NewRect(name, barRt);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 0.5f);
            return rt;
        }

        private static Image Segment(string name)
        {
            Image image = SegmentRect(name).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static Image NewImage(string name, Transform parent)
        {
            Image image = NewRect(name, parent).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static TextMeshProUGUI NewText(string name, float size, FontStyles style)
        {
            var text = NewRect(name, rootRt).gameObject.AddComponent<TextMeshProUGUI>();
            if (font) text.font = font;
            if (fontMaterial) text.fontSharedMaterial = fontMaterial;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.richText = true;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
