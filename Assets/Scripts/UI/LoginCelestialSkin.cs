using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace XiuXian.Core
{
    // Compatibility facade for the live login and server-selection flow.
    public static class LoginCelestialSkin
    {
        public const string Primary = "button_primary";
        public const string Row = "selector_row";
        public const string Panel = "dialog_panel";
        public static readonly Color Silver = QuietInkUI.Paper;

        public static RectTransform Rect(Transform p, string name, float x, float y, float w, float h)
            => QuietInkUI.Rect(p, name, x, y, w, h);

        public static Image Art(Transform p, string id, float x, float y, float w, float h, bool fit = false)
            => QuietInkUI.Art(p, id, x, y, w, h, fit);

        public static TMP_Text Text(Transform p, string name, string value, float x, float y,
            float w, float h, int size = 28, bool dark = false)
            => QuietInkUI.Text(p, name, value, x, y, w, h, size, QuietInkUI.Ink);

        public static Button Button(Transform p, string name, string label, float x, float y,
            float w, float h, UnityAction action, string art = Row, int size = 32)
            => QuietInkUI.Button(p, name, label, x, y, w, h, action, art, size);

        public static RectTransform Scroll(Transform p, string name, float x, float y, float w, float h)
            => QuietInkUI.Scroll(p, name, x, y, w, h);
    }
}
