using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>
    /// 齿轮图标（右上角设置入口用）。
    ///
    /// 刻意不用字体里的 "⚙"(U+2699)：MSYaHei SDF 是烘焙好的字体资源，而源字体
    /// msyh.ttc 因为格式不受 Unity 支持无法再补字形，这个字符极可能显示成缺字方块。
    /// 画成贴图则与字体覆盖范围无关，任何分辨率下都稳定。
    ///
    /// 贴图运行时生成一次并被所有实例共用，画成纯白，颜色由 color 决定。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class UIGearIcon : MonoBehaviour
    {
        [SerializeField] private Color color = new Color(0.18f, 0.12f, 0.04f, 1f);

        /// <summary>贴图边长（像素）。够画清楚齿形即可，反正是矢量感的图形。</summary>
        private const int TextureSize = 64;

        /// <summary>每个像素的超采样数（每轴）。靠它做抗锯齿，省掉距离场推导。</summary>
        private const int SamplesPerAxis = 4;

        private const float HoleRadiusRatio = 0.16f;
        private const float BodyRadiusRatio = 0.34f;
        private const float ToothRadiusRatio = 0.46f;
        private const int ToothCount = 8;

        /// <summary>每个扇区里齿占的比例，剩下的就是齿缝。要 &lt; 0.5 齿才显得分明。</summary>
        private const float ToothFillRatio = 0.42f;

        private static Sprite _sharedSprite;

        private void Awake()
        {
            Apply();
        }

        /// <summary>把齿轮贴图套到这个 Image 上。重复调用是幂等的。</summary>
        public void Apply()
        {
            var image = GetComponent<Image>();
            if (image == null)
            {
                return;
            }

            image.sprite = GetSharedSprite();
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
            // 图标只是装饰，不要抢走按钮的点击。
            image.raycastTarget = false;
        }

        private static Sprite GetSharedSprite()
        {
            if (_sharedSprite != null)
            {
                return _sharedSprite;
            }

            const float center = TextureSize * 0.5f;
            const float holeRadius = TextureSize * HoleRadiusRatio;
            const float bodyRadius = TextureSize * BodyRadiusRatio;
            const float toothRadius = TextureSize * ToothRadiusRatio;
            const float sector = Mathf.PI * 2f / ToothCount;
            const int totalSamples = SamplesPerAxis * SamplesPerAxis;

            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "UIGearIcon",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < SamplesPerAxis; sy++)
                    {
                        for (int sx = 0; sx < SamplesPerAxis; sx++)
                        {
                            float px = x + (sx + 0.5f) / SamplesPerAxis - center;
                            float py = y + (sy + 0.5f) / SamplesPerAxis - center;
                            float radius = Mathf.Sqrt(px * px + py * py);

                            if (radius < holeRadius || radius > toothRadius)
                            {
                                continue; // 中心孔与齿顶以外都留空
                            }
                            if (radius <= bodyRadius)
                            {
                                hits++; // 齿轮本体
                                continue;
                            }
                            // 本体之外只有落在"齿"的扇区里才算数，否则是齿缝。
                            float phase = Mathf.Repeat(Mathf.Atan2(py, px) / sector, 1f);
                            if (phase < ToothFillRatio)
                            {
                                hits++;
                            }
                        }
                    }

                    byte alpha = (byte)Mathf.RoundToInt(255f * hits / totalSamples);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            // 用 FullRect 而不是默认的 Tight：齿轮四角是透明的，Tight 会裁出贴合形状的
            // 网格，和 preserveAspect 配合时边界不直观。
            _sharedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            _sharedSprite.name = texture.name;
            return _sharedSprite;
        }
    }
}
