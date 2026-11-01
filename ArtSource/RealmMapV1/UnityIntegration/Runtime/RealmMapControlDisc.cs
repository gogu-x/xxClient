using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.RealmMap
{
    /// <summary>Quiet, resolution-independent backing for map controls.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RealmMapControlDisc : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds = rectTransform.rect;
            Vector2 center = bounds.center;
            float radius = Mathf.Min(bounds.width, bounds.height) * .5f;
            const int segments = 64;
            mesh.AddVert(center, new Color32(246, 242, 231, 247), Vector2.zero);
            for (int i = 0; i <= segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                mesh.AddVert(center + direction * (radius - 1.5f), new Color32(246, 242, 231, 247), Vector2.zero);
                mesh.AddVert(center + direction * radius, new Color32(155, 127, 80, 255), Vector2.zero);
                if (i == 0) continue;
                int inner = 1 + i * 2;
                mesh.AddTriangle(0, inner - 2, inner);
                mesh.AddTriangle(inner - 2, inner - 1, inner + 1);
                mesh.AddTriangle(inner - 2, inner + 1, inner);
            }
        }
    }
}
