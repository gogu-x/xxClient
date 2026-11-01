using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>Shared typography and controls; each destination owns its layout and palette.</summary>
    public static class FeaturePageKit
    {
        public static readonly Color Paper = new Color(.97f, .94f, .86f);
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y); r.sizeDelta = new Vector2(w, h);
            return r;
        }
        public static Image Box(Transform p, string name, float x, float y, float w, float h, Color c)
        {
            var image = Rect(p, name, x, y, w, h).gameObject.AddComponent<Image>();
            image.color = c; image.raycastTarget = false; return image;
        }
        public static TMP_Text Text(Transform p, string name, string value, float x, float y, float w, float h, int size = 24, Color? color = null)
        {
            var t = Rect(p, name, x, y, w, h).gameObject.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset; t.text = value; t.fontSize = size;
            t.color = color ?? Paper; t.raycastTarget = false; t.richText = false;
            t.overflowMode = TextOverflowModes.Ellipsis; t.alignment = TextAlignmentOptions.MidlineLeft;
            return t;
        }
        public static Button Button(Transform p, string name, string label, float x, float y, float w, float h, Color c, UnityAction action)
        {
            var im = Box(p, name, x, y, w, h, c); im.raycastTarget = true;
            var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im;
            var colors = b.colors; colors.highlightedColor = new Color(1,.91f,.75f); colors.pressedColor = new Color(.7f,.7f,.7f); b.colors = colors;
            b.onClick.AddListener(action);
            var t = Text(im.transform, "Label", label, 12, 0, w-24, h, 22); t.alignment = TextAlignmentOptions.Center;
            return b;
        }
        public static TMP_InputField Input(Transform p, string name, string hint, float x, float y, float w, Color c)
        {
            var im = Box(p, name, x, y, w, 54, c); im.raycastTarget = true;
            var input = im.gameObject.AddComponent<TMP_InputField>(); input.targetGraphic = im;
            var text = Text(im.transform, "Text", "", 16, 2, w-32, 50, 21);
            var placeholder = Text(im.transform, "Placeholder", hint, 16, 2, w-32, 50, 21, new Color(.7f,.7f,.7f));
            input.textViewport = (RectTransform)im.transform; input.textComponent = (TextMeshProUGUI)text;
            input.placeholder = placeholder; input.characterLimit = 40; return input;
        }
        public static RectTransform Scroll(Transform p, string name, float x, float y, float w, float h, float contentHeight)
        {
            var viewport = Rect(p, name, x, y, w, h); viewport.gameObject.AddComponent<RectMask2D>();
            var image = viewport.gameObject.AddComponent<Image>(); image.color = new Color(0,0,0,.01f);
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var content = Rect(viewport, "Content", 0, 0, w, Mathf.Max(h, contentHeight));
            scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            return content;
        }
        public static void Background(Transform p, string resource, Color fallback)
        {
            var texture = Resources.Load<Texture2D>(resource);
            if (texture != null)
            {
                var raw = Rect(p, "PageArtwork", 0, 0, 1600, 900).gameObject.AddComponent<RawImage>();
                raw.texture = texture; raw.raycastTarget = false;
            }
            else Box(p, "PageArtwork", 0, 0, 1600, 900, fallback);
        }
    }

}
