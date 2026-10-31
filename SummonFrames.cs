using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummonCommander
{
    // One summon: a single row in its species' panel.
    internal struct SummonRowData
    {
        // Number and, when there is one, the summon's own name and a skeleton's weapon.
        public string Label;
        // Distance and state, right-aligned.
        public string Info;
        public Color SpeciesColor;
        public bool ShowBar;
        public float Health;
        public float MaxHealth;
        public Color BarColor;
        // Barrier from the Staff of Protection, when the summon has one.
        public bool ShowShield;
        public float Shield;
        public float MaxShield;
        public float ShieldSeconds;
    }

    // One species (skeletons, wolves, ...): a panel with a header and a row per summon.
    internal class SummonGroupData
    {
        public string Header;
        public List<SummonRowData> Rows;
    }

    // Party-frame style panels below the summon counter: a translucent panel per species with a header line
    // and one compact row per summon (species color stripe, label, HP bar, distance and state). Panels and rows
    // are pooled and reused on every scan. Sizes scale with the HUD font size, and the whole list shrinks when
    // it would run off the bottom of the screen.
    internal class SummonFrames
    {
        private const float Gap = 6f;
        // Kept free at the bottom of the screen, where the game shows key hints.
        private const float BottomReserve = 0.08f;
        private const float MinScale = 0.4f;
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color BarBackColor = new Color(0.08f, 0.08f, 0.08f, 0.85f);
        private static readonly Color ShieldColor = new Color(0.45f, 0.75f, 1f);

        private class Bar
        {
            public RectTransform Back;
            public RectTransform Fill;
            public Image FillImage;
            public TextMeshProUGUI Text;
        }

        private class Row
        {
            public RectTransform Root;
            public Image Stripe;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Info;
            public Bar Health;
            public Bar Shield;
        }

        private class Panel
        {
            public RectTransform Root;
            public TextMeshProUGUI Header;
            public readonly List<Row> Rows = new List<Row>();
        }

        private readonly List<Panel> _panels = new List<Panel>();
        private RectTransform _container;
        // Never shows anything; measures the labels and headers (same font) to size the columns.
        private TextMeshProUGUI _measure;

        // anchor: the HUD box's anchor; top: distance from that anchor down to the first panel; infoWidth: width of
        // the rows' right column in font sizes (0 when it shows nothing), which the HP bar gives up; panelWidth: the
        // panels' width, or 0 to fit the longest label.
        public void Show(List<SummonGroupData> groups, Vector2 anchor, float rightMargin, float top, float fontSize,
            float infoWidth, float panelWidth)
        {
            if (_container == null && !CreateContainer())
                return;

            var size = new Sizes(fontSize, infoWidth);
            // Each panel's name column fits its longest label, so spirit panels (labels like "#5") give the space
            // to their HP bars. All panels are as wide as the longest label, its bar and the longest header need.
            var labelWidths = groups.Select(group => group.Rows.Count == 0 ? 0f
                : group.Rows.Max(row => Measure(row.Label, size.Font * LabelFont)) + size.Font * 0.2f).ToList();
            float width = panelWidth > 0f
                ? Mathf.Max(panelWidth, size.RowChrome + size.Font * 4f)
                : Mathf.Max(labelWidths.Max() + size.RowChrome + size.MinBarWidth,
                    groups.Max(group => Measure(group.Header, size.Font * HeaderFont)) + size.Padding * 2f + size.Font * 0.2f);
            // In a set width the bar keeps room for its numbers ("1500 / 1500", or half the space when even that
            // does not fit) and the names get the rest; text that still does not fit shrinks (see SetText), then
            // gets an ellipsis.
            float available = width - size.RowChrome;
            float barNeeds = groups.Max(group => group.Rows.Count == 0 ? 0f
                : group.Rows.Max(row => Measure(HealthText(row.MaxHealth, row.MaxHealth), size.Font * BarFont)))
                + size.Font * 0.6f;
            float maxLabelWidth = available - Mathf.Min(barNeeds, available * 0.5f);
            for (int i = 0; i < labelWidths.Count; i++)
                labelWidths[i] = Mathf.Min(labelWidths[i], maxLabelWidth);
            _container.anchorMin = _container.anchorMax = anchor;
            _container.anchoredPosition = new Vector2(-rightMargin, -top);
            _container.gameObject.SetActive(true);

            float y = 0f;
            for (int i = 0; i < groups.Count; i++)
            {
                if (i == _panels.Count)
                    _panels.Add(CreatePanel(_container));
                var panel = _panels[i];
                panel.Root.gameObject.SetActive(true);
                panel.Root.anchoredPosition = new Vector2(0f, -y);
                float height = LayoutPanel(panel, groups[i], size, width, labelWidths[i]);
                panel.Root.sizeDelta = new Vector2(width, height);
                y += height + Gap;
            }
            for (int i = groups.Count; i < _panels.Count; i++)
                _panels[i].Root.gameObject.SetActive(false);

            FitOnScreen(anchor, top, y);
        }

        private const float LabelFont = 0.9f;
        private const float HeaderFont = 1.05f;
        private const float BarFont = 0.8f;

        private static string HealthText(float health, float maxHealth)
        {
            return $"{health:0} / {maxHealth:0}";
        }

        private float Measure(string text, float fontSize)
        {
            _measure.fontSize = fontSize;
            return _measure.GetPreferredValues(text).x;
        }

        // Sizes derived from the HUD font size.
        private readonly struct Sizes
        {
            public readonly float Font;
            public readonly float Padding;
            public readonly float HeaderHeight;
            public readonly float RowHeight;
            public readonly float StripeWidth;
            public readonly float MinBarWidth;
            public readonly float RowChrome;
            public readonly float InfoWidth;
            public readonly float ShieldHeight;

            public Sizes(float font, float infoWidth)
            {
                Font = font;
                Padding = font * 0.35f;
                HeaderHeight = font * 1.4f;
                RowHeight = font * 1.15f;
                StripeWidth = font * 0.3f;
                InfoWidth = font * infoWidth;
                MinBarWidth = font * 9f;
                // A row's width besides its name column and HP bar: the panel's padding, the stripe and the gaps
                // (see LayoutRow), and the right column.
                RowChrome = Padding * 4f + StripeWidth + InfoWidth;
                ShieldHeight = font * 0.6f;
            }
        }

        // Returns the panel's height.
        private static float LayoutPanel(Panel panel, SummonGroupData group, Sizes size, float panelWidth, float labelWidth)
        {
            SetText(panel.Header, group.Header, size.Padding, size.Padding * 0.5f, panelWidth - size.Padding * 2f,
                size.HeaderHeight, size.Font * HeaderFont);

            float y = size.Padding * 0.5f + size.HeaderHeight;
            for (int i = 0; i < group.Rows.Count; i++)
            {
                if (i == panel.Rows.Count)
                    panel.Rows.Add(CreateRow(panel.Root));
                var row = panel.Rows[i];
                row.Root.gameObject.SetActive(true);
                row.Root.anchoredPosition = new Vector2(size.Padding, -y);
                float height = LayoutRow(row, group.Rows[i], size, panelWidth, labelWidth);
                y += height + size.Padding * 0.4f;
            }
            for (int i = group.Rows.Count; i < panel.Rows.Count; i++)
                panel.Rows[i].Root.gameObject.SetActive(false);
            return y + size.Padding * 0.6f;
        }

        // [stripe][label        ][HP bar          ][     info]; the shield bar, when there is one, goes below
        // the HP bar. Returns the row's height.
        private static float LayoutRow(Row row, SummonRowData item, Sizes size, float panelWidth, float labelWidth)
        {
            float width = panelWidth - size.Padding * 2f;
            float height = size.RowHeight + (item.ShowShield ? size.ShieldHeight + size.Padding * 0.25f : 0f);
            row.Root.sizeDelta = new Vector2(width, height);

            var stripe = row.Stripe.rectTransform;
            stripe.anchoredPosition = Vector2.zero;
            stripe.sizeDelta = new Vector2(size.StripeWidth, height);
            row.Stripe.color = item.SpeciesColor;

            float labelX = size.StripeWidth + size.Padding;
            SetText(row.Label, item.Label, labelX, 0f, labelWidth, size.RowHeight, size.Font * LabelFont);
            SetText(row.Info, item.Info, width - size.InfoWidth, 0f, size.InfoWidth, size.RowHeight, size.Font * 0.85f);

            float barX = labelX + labelWidth + size.Padding * 0.5f;
            float barWidth = width - size.InfoWidth - size.Padding * 0.5f - barX;
            float barHeight = size.RowHeight * 0.85f;
            float barY = (size.RowHeight - barHeight) * 0.5f;
            SetBar(row.Health, item.ShowBar, item.Health, item.MaxHealth, item.BarColor,
                HealthText(item.Health, item.MaxHealth), barX, barY, barWidth, barHeight, size.Font * BarFont);
            SetBar(row.Shield, item.ShowShield, item.Shield, item.MaxShield, ShieldColor,
                $"{item.Shield:0} · {item.ShieldSeconds:0} s", barX, size.RowHeight + size.Padding * 0.25f, barWidth,
                size.ShieldHeight, size.Font * 0.55f);
            return height;
        }

        // Places a one-line text in the slot (x, y, width, height), vertically centered. The text's box is taller
        // than the slot: the ellipsis also cuts text that overflows vertically, and letters the HUD font lacks
        // (ś, ź, ...) come from a fallback font with a taller line, so "Niedźwiedź" became "Nie..." in a box
        // exactly one line high. Only the width may cut the text.
        private static void SetText(TextMeshProUGUI text, string value, float x, float y, float width, float height,
            float fontSize)
        {
            text.text = value;
            SetFontSize(text, fontSize);
            text.rectTransform.anchoredPosition = new Vector2(x, -(y + height * 0.5f));
            text.rectTransform.sizeDelta = new Vector2(width, height * 3f);
        }

        private static void SetBar(Bar bar, bool show, float value, float max, Color color, string text,
            float x, float y, float width, float height, float fontSize)
        {
            bar.Back.gameObject.SetActive(show);
            if (!show)
                return;
            bar.Back.anchoredPosition = new Vector2(x, -y);
            bar.Back.sizeDelta = new Vector2(width, height);
            bar.Fill.anchorMax = new Vector2(max > 0f ? Mathf.Clamp01(value / max) : 0f, 1f);
            bar.FillImage.color = color;
            bar.Text.text = text;
            SetFontSize(bar.Text, fontSize);
            // Taller than the bar, so only the width can shrink the text (see SetText).
            bar.Text.rectTransform.offsetMin = new Vector2(0f, -height);
            bar.Text.rectTransform.offsetMax = new Vector2(0f, height);
        }

        // Text that does not fit its width shrinks down to MinFontScale of its size before the ellipsis cuts it,
        // so narrow panels (HUD.PanelWidth) keep their content readable.
        private const float MinFontScale = 0.7f;

        private static void SetFontSize(TextMeshProUGUI text, float fontSize)
        {
            text.enableAutoSizing = true;
            text.fontSizeMax = fontSize;
            text.fontSizeMin = fontSize * MinFontScale;
            text.fontSize = fontSize;
        }

        // Shrinks the panels (from their top right corner) when they would run past the bottom of the screen,
        // e.g. with a full Spirit Caller pack and skeletons.
        private void FitOnScreen(Vector2 anchor, float top, float height)
        {
            var parent = (RectTransform)_container.parent;
            float screenHeight = parent.rect.height;
            float available = screenHeight * (anchor.y - BottomReserve) - top;
            float scale = height > available && available > 0f ? Mathf.Max(MinScale, available / height) : 1f;
            _container.localScale = new Vector3(scale, scale, 1f);
        }

        public void Hide()
        {
            if (_container != null)
                _container.gameObject.SetActive(false);
        }

        public void Destroy()
        {
            if (_container != null)
                Object.Destroy(_container.gameObject);
            _container = null;
            _panels.Clear();
        }

        private bool CreateContainer()
        {
            // The previous container went away with the previous HUD, and its panels with it.
            _panels.Clear();
            var hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return false;
            _container = CreateRect("SummonCommanderFrames", hud.m_rootObject.transform, new Vector2(1f, 1f));
            // Draw beneath the other HUD elements, like the counter.
            _container.SetAsFirstSibling();
            _measure = CreateLeftText("Measure", _container, TextAlignmentOptions.Left);
            _measure.text = "";
            return true;
        }

        private static Panel CreatePanel(Transform parent)
        {
            var panel = new Panel();
            panel.Root = CreateRect("Panel", parent, new Vector2(1f, 1f));
            panel.Root.anchorMin = panel.Root.anchorMax = new Vector2(1f, 1f);
            AddImage(panel.Root, null, PanelColor);

            panel.Header = CreateLeftText("Header", panel.Root, TextAlignmentOptions.Left);
            return panel;
        }

        private static Row CreateRow(Transform parent)
        {
            var row = new Row();
            row.Root = CreateRect("Row", parent, new Vector2(0f, 1f));
            row.Root.anchorMin = row.Root.anchorMax = new Vector2(0f, 1f);

            var stripe = CreateRect("Stripe", row.Root, new Vector2(0f, 1f));
            stripe.anchorMin = stripe.anchorMax = new Vector2(0f, 1f);
            row.Stripe = AddImage(stripe, null, Color.white);

            row.Label = CreateLeftText("Label", row.Root, TextAlignmentOptions.Left);
            row.Info = CreateLeftText("Info", row.Root, TextAlignmentOptions.Right);
            row.Health = CreateBar("HealthBar", row.Root);
            row.Shield = CreateBar("ShieldBar", row.Root);
            return row;
        }

        // Text positioned from its parent's top left corner, cut with an ellipsis when too long.
        private static TextMeshProUGUI CreateLeftText(string name, Transform parent, TextAlignmentOptions alignment)
        {
            var text = SummonHud.CreateText(name, parent, alignment, new Vector2(0f, 0.5f), Vector2.zero);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 1f);
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private static Bar CreateBar(string name, Transform parent)
        {
            // The fill reuses the sprite of the player's own health bar so it matches the game's style.
            var barSprite = GetBarSprite();
            var bar = new Bar();
            bar.Back = CreateRect(name, parent, new Vector2(0f, 1f));
            bar.Back.anchorMin = bar.Back.anchorMax = new Vector2(0f, 1f);
            AddImage(bar.Back, barSprite, BarBackColor);

            bar.Fill = CreateRect("Fill", bar.Back, new Vector2(0f, 0.5f));
            bar.Fill.anchorMin = Vector2.zero;
            bar.Fill.offsetMin = bar.Fill.offsetMax = Vector2.zero;
            bar.FillImage = AddImage(bar.Fill, barSprite, Color.white);

            bar.Text = SummonHud.CreateText("Text", bar.Back, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), Vector2.zero);
            var textRect = bar.Text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            return bar;
        }

        private static Sprite GetBarSprite()
        {
            var bar = Hud.instance != null ? Hud.instance.m_healthBarFast : null;
            var image = bar != null && bar.m_bar != null ? bar.m_bar.GetComponent<Image>() : null;
            return image != null ? image.sprite : null;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 pivot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            var rect = (RectTransform)go.transform;
            rect.pivot = pivot;
            return rect;
        }

        private static Image AddImage(RectTransform rect, Sprite sprite, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            // Bordered sprites stretch without distorting their edges when sliced.
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
    }
}
