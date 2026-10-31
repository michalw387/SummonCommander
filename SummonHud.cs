using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SummonCommander
{
    // Summon counter, list and frames drawn as part of the game's HUD canvas, so windows like
    // the inventory cover them the same way they cover the minimap, and they hide together with the HUD.
    internal class SummonHud
    {
        private const float RightMargin = 20f;

        private const float FramesGap = 6f;

        private readonly SummonFrames _frames = new SummonFrames();
        private TextMeshProUGUI _list;

        // frames: per-species panels below the text, or null to show only the text; infoWidth, panelWidth: see
        // SummonFrames.Show.
        public void Show(string text, float positionY, float fontSize, List<SummonGroupData> frames, float infoWidth,
            float panelWidth)
        {
            if (_list == null)
                _list = CreateHudText("SummonCommanderHud", TextAlignmentOptions.TopLeft, new Vector2(1f, 1f), new Vector2(-RightMargin, 0f));
            if (_list == null)
                return;

            _list.text = text;
            _list.fontSize = fontSize;
            var rect = _list.rectTransform;
            var anchor = new Vector2(1f, 1f - positionY);
            rect.anchorMin = rect.anchorMax = anchor;
            // Lines are left-aligned inside a box as wide as the longest line, and the box keeps a fixed
            // distance from the right screen edge, so the longest line ends at that margin.
            var size = _list.GetPreferredValues(text);
            rect.sizeDelta = new Vector2(Mathf.Ceil(size.x), Mathf.Ceil(size.y));
            _list.gameObject.SetActive(true);

            if (frames != null && frames.Count > 0)
                _frames.Show(frames, anchor, RightMargin, rect.sizeDelta.y + FramesGap, fontSize, infoWidth, panelWidth);
            else
                _frames.Hide();
        }

        public void Hide()
        {
            if (_list != null)
                _list.gameObject.SetActive(false);
            _frames.Hide();
        }

        public void Destroy()
        {
            if (_list != null)
                Object.Destroy(_list.gameObject);
            _list = null;
            _frames.Destroy();
        }

        private static TextMeshProUGUI CreateHudText(string name, TextAlignmentOptions alignment, Vector2 pivot, Vector2 offset)
        {
            var hud = Hud.instance;
            if (hud == null || hud.m_rootObject == null)
                return null;
            var text = CreateText(name, hud.m_rootObject.transform, alignment, pivot, offset);
            // Draw beneath the other HUD elements.
            text.transform.SetAsFirstSibling();
            return text;
        }

        // Requires Hud.instance, whose font the text reuses.
        public static TextMeshProUGUI CreateText(string name, Transform parent, TextAlignmentOptions alignment, Vector2 pivot, Vector2 offset)
        {
            var hud = Hud.instance;
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, worldPositionStays: false);
            // Inactive until the font is set, or TextMeshPro looks for its default font when it awakes.
            go.SetActive(false);

            var text = go.AddComponent<TextMeshProUGUI>();
            // Reuse the HUD's font and material (with its outline) so the text matches the game's style.
            text.font = hud.m_healthText.font;
            text.fontSharedMaterial = hud.m_healthText.fontSharedMaterial;
            text.fontSize = 18;
            text.alignment = alignment;
            text.richText = true;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;

            var rect = text.rectTransform;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(700f, 400f);
            rect.anchoredPosition = offset;
            go.SetActive(true);
            return text;
        }
    }
}
