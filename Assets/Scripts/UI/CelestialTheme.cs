using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>Shared celestial manhua palette; semantic rarity/status colors remain local.</summary>
    public static class CelestialTheme
    {
        public static readonly Color Ink = new(0.045f, 0.055f, 0.15f, 0.88f);
        public static readonly Color Gold = new(0.89f, 0.78f, 0.56f, 1f);
        public static readonly Color Light = new(0.91f, 0.93f, 1f, 1f);
        public static readonly Color Blue = new(0.57f, 0.76f, 1f, 1f);
        public static readonly Color Violet = new(0.77f, 0.62f, 1f, 1f);

        private static readonly float[] IconLeft = { 24f, 347f, 660f, 970f, 1281f };
        private static readonly float[] IconRight = { 347f, 660f, 970f, 1281f, 1600f };
        private static readonly float[] IconTop = { 12f, 326f, 640f };
        private static readonly float[] IconBottom = { 324f, 639f, 950f };

        public static Rect IconUv(int index)
        {
            index = Mathf.Clamp(index, 0, 14);
            int column = index % 5;
            int row = index / 5;
            return new Rect(IconLeft[column] / 1619f, 1f - IconBottom[row] / 971f,
                (IconRight[column] - IconLeft[column]) / 1619f,
                (IconBottom[row] - IconTop[row]) / 971f);
        }
    }
}
