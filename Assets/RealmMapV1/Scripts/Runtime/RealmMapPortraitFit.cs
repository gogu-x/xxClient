using UnityEngine;

namespace XiuXian.RealmMap
{
    [ExecuteAlways]
    public sealed class RealmMapPortraitFit : MonoBehaviour
    {
        private void LateUpdate()
        {
            var rect = transform as RectTransform;
            var parent = rect == null ? null : rect.parent as RectTransform;
            if (parent == null || parent.rect.width <= 0 || parent.rect.height <= 0) return;
            Rect safe = Screen.safeArea;
            float sw = Screen.width > 0 ? safe.width / Screen.width : 1;
            float sh = Screen.height > 0 ? safe.height / Screen.height : 1;
            float scale = Mathf.Min(parent.rect.width * sw / RealmMapArt.DesignWidth,
                parent.rect.height * sh / RealmMapArt.DesignHeight);
            rect.localScale = Vector3.one * Mathf.Max(.01f, scale);
            if (Screen.width > 0 && Screen.height > 0)
                rect.anchoredPosition = new Vector2((safe.center.x / Screen.width - .5f) * parent.rect.width,
                    (safe.center.y / Screen.height - .5f) * parent.rect.height);
        }
    }
}
