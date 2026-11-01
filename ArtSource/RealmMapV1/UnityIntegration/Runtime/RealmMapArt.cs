using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XiuXian.RealmMap
{
    [Serializable]
    public sealed class RealmMapLayout
    {
        public float map_width = 3840, map_height = 2560;
        public bool normalized;
        public float initial_center_x = .5f, initial_center_y = .5f;
        public float initial_zoom = 1.1f, min_zoom = .7f, max_zoom = 1.6f;
        public string title = "山海秘境", region = "青云州";
        public RealmMapNode[] nodes;
    }

    [Serializable]
    public sealed class RealmMapNode
    {
        public string id, name, art;
        public float x, y, width = 240, height = 260;
        public string region, description;
        public bool show_label;
    }

    /// <summary>Independent resource resolver; no battle, account, save or backend dependency.</summary>
    public static class RealmMapArt
    {
        public const string ResourceRoot = "UI/RealmMapV1/";
        public const float DesignWidth = 941, DesignHeight = 1672;
        public static readonly Color Paper = new Color32(246, 242, 231, 255);
        public static readonly Color Ink = new Color32(35, 47, 43, 255);
        public static readonly Color Jade = new Color32(41, 112, 108, 255);
        public static readonly Color Muted = new Color32(111, 113, 103, 255);
        public static readonly Color Rule = new Color32(187, 164, 122, 170);
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static bool loaded;
        private static TMP_FontAsset font, displayFont;
        public static void ClearCache() { Sprites.Clear(); loaded = false; }
        public static Sprite Sprite(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!loaded)
            {
                foreach (var sprite in Resources.LoadAll<Sprite>(ResourceRoot))
                    if (sprite != null) Sprites[sprite.name] = sprite;
                loaded = true;
            }
            return Sprites.TryGetValue(id, out var result) ? result : null;
        }
        public static RealmMapLayout LoadLayout()
        {
            var asset = Resources.Load<TextAsset>(ResourceRoot + "world-layout");
            return asset == null ? null : JsonUtility.FromJson<RealmMapLayout>(asset.text);
        }
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        public static Image Art(Transform parent, string id, float x, float y, float width, float height, bool preserveAspect = true)
        {
            var image = Rect(parent, id, x, y, width, height).gameObject.AddComponent<Image>();
            image.sprite = Sprite(id);
            image.preserveAspect = preserveAspect;
            image.color = image.sprite == null ? Color.clear : Color.white;
            image.raycastTarget = false;
            if (!preserveAspect && image.sprite != null && image.sprite.border.sqrMagnitude > 0)
                image.type = Image.Type.Sliced;
            return image;
        }
        public static Image Fill(Transform parent, string name, float x, float y, float width, float height, Color color)
        {
            var image = Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }
        public static TMP_Text Text(Transform parent, string name, string value, float x, float y, float width, float height,
            float size = 28, Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            if (font == null) font = Resources.Load<TMP_FontAsset>("Fonts/MSYaHei SDF") ?? TMP_Settings.defaultFontAsset;
            if (displayFont == null) displayFont = Resources.Load<TMP_FontAsset>("Fonts/LobbyDisplay") ?? font;
            var text = Rect(parent, name, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = size >= 38 || name == "EntranceName" ? displayFont : font;
            text.text = value ?? string.Empty;
            text.fontSize = size;
            text.color = color ?? Ink;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }
        public static Button Button(Transform parent, string name, string label, float x, float y, float width, float height,
            UnityAction action, bool filled = false, float textSize = 28)
        {
            var image = Fill(parent, name, x, y, width, height, filled ? Jade : Color.clear);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = Text(image.transform, "Label", label, 8, 4, width - 16, height - 8,
                textSize, filled ? Paper : Ink);
            text.enableAutoSizing = true;
            text.fontSizeMin = 20;
            text.fontSizeMax = textSize;
            if (action != null) button.onClick.AddListener(action);
            return button;
        }
    }
}
