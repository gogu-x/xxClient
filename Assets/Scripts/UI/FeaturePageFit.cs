using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>Fit the entire 16:9 design into the available canvas without cropping controls.</summary>
    public sealed class FeaturePageFit : MonoBehaviour
    {
        private void LateUpdate()
        {
            var r = (RectTransform)transform; var p = r.parent as RectTransform;
            if (p == null) return;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f);
            r.sizeDelta = new Vector2(1600,900); r.anchoredPosition = Vector2.zero;
            float s = Mathf.Min(p.rect.width / 1600f, p.rect.height / 900f);
            r.localScale = Vector3.one * Mathf.Max(.01f,s);
        }
    }
}
