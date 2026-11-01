using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>
    /// 统一的按钮外观：圆角 + 描边 + 纵向渐变，以及一致的悬停/按下/禁用反馈。
    ///
    /// 贴图在运行时按需生成一次、由所有按钮共用，并且画成**纯白**——最终颜色完全由
    /// Image.color 决定。所以不同用途的按钮只需要给不同的颜色，形状和质感自动保持一致，
    /// 不会出现"这个按钮是圆角那个是直角"的情况。
    ///
    /// 注意：贴图是运行时生成的，所以只在 Play 模式下可见；编辑器的 Scene 视图里
    /// 按钮仍然是未换肤的方块，这是预期行为。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonSkin : MonoBehaviour
    {
        /// <summary>
        /// 按钮整体缩放倍率。各页面写的是"设计尺寸"，实际尺寸都会乘上这个值，
        /// 所以嫌按钮大／小只需要改这一个地方，登录页、大厅、战斗页会一起变。
        /// 注意边框和圆角不跟着缩放（它们由贴图决定），这是有意的 —— 否则小按钮的
        /// 描边会细到看不见。
        /// </summary>
        public const float Scale = 0.85f;

        /// <summary>圆角半径（UI 参考像素）。所有按钮共用同一个值，形状才会统一。</summary>
        private const int CornerRadius = 18;

        /// <summary>描边宽度（UI 参考像素）。描边由贴图烘进去，靠压暗边缘实现。</summary>
        private const float BorderWidth = 2f;

        /// <summary>顶部相对底部的亮度，做出一点体积感。</summary>
        private const float TopShade = 1f;
        private const float BottomShade = 0.84f;

        private static Sprite _sharedSprite;

        private void Awake()
        {
            Apply();
        }

        /// <summary>把统一外观套到这个按钮上。重复调用是幂等的。</summary>
        public void Apply()
        {
            var button = GetComponent<Button>();
            if (button == null)
            {
                return;
            }

            // 只处理纯色底的按钮。贴图按钮（如角色面板的返回键）保持原样，
            // 否则会把它的美术图案冲掉。
            var image = button.targetGraphic as Image;
            if (image == null || image.sprite != null)
            {
                return;
            }

            image.sprite = GetSharedSprite();
            image.type = Image.Type.Sliced;
            // Sliced 模式下圆角按 贴图像素 / pixelsPerUnitMultiplier 换算，
            // 这里保持 1，让圆角在参考分辨率下就是 CornerRadius 像素。
            image.pixelsPerUnitMultiplier = 1f;

            button.transition = Selectable.Transition.ColorTint;
            button.colors = BuildColorBlock(button.colors);
        }

        /// <summary>
        /// 统一的交互反馈。必须显式覆盖 Unity 的默认色块——它的 highlightedColor
        /// 是浅灰，叠在本就接近白色的按钮上会显得发暗。
        /// </summary>
        private static ColorBlock BuildColorBlock(ColorBlock current)
        {
            current.colorMultiplier = 1f;
            current.fadeDuration = 0.08f;
            current.normalColor = Color.white;
            current.highlightedColor = Color.white;
            current.pressedColor = new Color(0.80f, 0.80f, 0.80f, 1f);
            current.selectedColor = Color.white;
            current.disabledColor = new Color(0.58f, 0.58f, 0.58f, 0.55f);
            return current;
        }

        /// <summary>
        /// 生成圆角矩形贴图：九宫格四边都切，中间可拉伸，所以按钮拉成任何尺寸圆角都不变形。
        /// 颜色画成灰阶（边缘暗、中间白），由 Image.color 上色。
        /// </summary>
        private static Sprite GetSharedSprite()
        {
            if (_sharedSprite != null)
            {
                return _sharedSprite;
            }

            int size = CornerRadius * 2 + 28; // 中间留出一段可拉伸区域
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "UIButtonRounded",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float boxHalf = half - CornerRadius; // 圆角矩形"骨架"的半边长

            for (int y = 0; y < size; y++)
            {
                float py = y + 0.5f - half;
                float gradient = Mathf.Lerp(BottomShade, TopShade, (y + 0.5f) / size);
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;

                    // 圆角矩形的有符号距离场：内部为负、边界为 0、外部为正。
                    float dx = Mathf.Abs(px) - boxHalf;
                    float dy = Mathf.Abs(py) - boxHalf;
                    float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) +
                                               Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                    float distance = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - CornerRadius;

                    float alpha = Mathf.Clamp01(0.5f - distance); // 边缘 1px 抗锯齿
                    float borderT = Mathf.Clamp01(1f - (-distance) / BorderWidth);
                    byte value = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(Mathf.Lerp(1f, 0.55f, borderT) * gradient) * 255f);
                    pixels[y * size + x] = new Color32(value, value, value,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            // pixelsPerUnit = 100 与 Canvas 的参考 PPU 一致，圆角大小才是所见即所得。
            _sharedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(CornerRadius, CornerRadius, CornerRadius, CornerRadius));
            _sharedSprite.name = texture.name;
            return _sharedSprite;
        }
    }
}
