using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>已修习节点的固定金环、柔光与勾选印记，避免仅靠图标明暗区分。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class TalentNodeHighlight : MaskableGraphic
    {
        private bool learned, selected, ready;

        public void SetState(bool isLearned, bool isSelected, bool isReady)
        {
            if (learned == isLearned && selected == isSelected && ready == isReady) return;
            learned = isLearned; selected = isSelected; ready = isReady;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Vector2 center = rectTransform.rect.center;
            float radius = Mathf.Min(rectTransform.rect.width, rectTransform.rect.height) * .5f;
            if (radius <= 0) return;
            if (learned)
            {
                Ring(mesh, center, radius * .84f, radius * .98f,
                    new Color(1f, .77f, .30f, .30f), new Color(1f, .77f, .30f, 0));
                Ring(mesh, center, radius * .78f, radius * .84f,
                    new Color(.80f, .56f, .19f, 1), new Color(1f, .87f, .49f, 1));
                Ring(mesh, center, radius * .72f, radius * .745f,
                    new Color(1f, .91f, .61f, .95f), new Color(1f, .91f, .61f, .95f));
                Vector2 seal = center + new Vector2(radius * .55f, -radius * .55f);
                Disk(mesh, seal, radius * .19f, new Color(.73f, .51f, .19f, 1));
                Disk(mesh, seal, radius * .155f, new Color(1, .92f, .66f, 1));
                Stroke(mesh, seal + new Vector2(-7, 0), seal + new Vector2(-1, -5), 3.2f, QuietInkUI.Jade);
                Stroke(mesh, seal + new Vector2(-1, -5), seal + new Vector2(9, 7), 3.2f, QuietInkUI.Jade);
            }
            else if (ready)
                Ring(mesh, center, radius * .78f, radius * .805f,
                    new Color(.20f, .53f, .48f, .62f), new Color(.20f, .53f, .48f, .62f));
            if (selected)
                Ring(mesh, center, radius * .925f, radius * .95f,
                    new Color(.15f, .52f, .51f, .95f), new Color(.15f, .52f, .51f, .95f));
        }

        private static void Ring(VertexHelper mesh, Vector2 center, float inner, float outer, Color innerColor, Color outerColor)
        {
            const int count = 64;
            int start = mesh.currentVertCount;
            for (int i = 0; i <= count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * inner, innerColor, Vector2.zero);
                mesh.AddVert(center + direction * outer, outerColor, Vector2.zero);
                if (i == 0) continue;
                int previous = start + (i - 1) * 2, current = start + i * 2;
                mesh.AddTriangle(previous, current, previous + 1);
                mesh.AddTriangle(previous + 1, current, current + 1);
            }
        }

        private static void Disk(VertexHelper mesh, Vector2 center, float radius, Color tint)
        {
            const int count = 32;
            int start = mesh.currentVertCount;
            mesh.AddVert(center, tint, Vector2.zero);
            for (int i = 0; i <= count; i++)
            {
                float angle = i * Mathf.PI * 2 / count;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                if (i > 0) mesh.AddTriangle(start, start + i, start + i + 1);
            }
        }

        private static void Stroke(VertexHelper mesh, Vector2 from, Vector2 to, float width, Color tint)
        {
            Vector2 direction = (to - from).normalized;
            Vector2 side = new Vector2(-direction.y, direction.x) * width * .5f;
            int start = mesh.currentVertCount;
            mesh.AddVert(from - side, tint, Vector2.zero);
            mesh.AddVert(from + side, tint, Vector2.zero);
            mesh.AddVert(to + side, tint, Vector2.zero);
            mesh.AddVert(to - side, tint, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
