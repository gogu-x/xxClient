using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>A quiet paper surface for compact information, without ornamental sprite borders.</summary>
    public static class CompactInfoCard
    {
        public static Image Create(Transform parent, string name, float x, float y, float width, float height)
        {
            var image = QuietInkUI.Rect(parent, name, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = new Color32(249, 246, 238, 242);
            image.raycastTarget = false;
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color32(188, 183, 166, 105);
            outline.effectDistance = new Vector2(1, -1);
            outline.useGraphicAlpha = true;
            return image;
        }
    }
}
