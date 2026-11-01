using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>
    /// Fits the logical UI canvas to the limiting screen dimension. The canvas reference
    /// resolution is only a layout coordinate system; the rendered canvas follows the
    /// current device resolution and aspect ratio.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    public sealed class MobileAdaptiveCanvasScaler : MonoBehaviour
    {
        private CanvasScaler scaler;
        private int lastWidth = -1;
        private int lastHeight = -1;

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            ApplyForCurrentScreen();
        }

        private void OnEnable()
        {
            if (scaler == null) scaler = GetComponent<CanvasScaler>();
            ApplyForCurrentScreen();
        }

        private void Update()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight) return;
            ApplyForCurrentScreen();
        }

        private void ApplyForCurrentScreen()
        {
            if (scaler == null || Screen.width <= 0 || Screen.height <= 0) return;

            Vector2 reference = scaler.referenceResolution;
            if (reference.x <= 0f || reference.y <= 0f) return;

            float widthScale = Screen.width / reference.x;
            float heightScale = Screen.height / reference.y;
            scaler.matchWidthOrHeight = widthScale < heightScale ? 0f : 1f;

            lastWidth = Screen.width;
            lastHeight = Screen.height;
        }
    }
}
