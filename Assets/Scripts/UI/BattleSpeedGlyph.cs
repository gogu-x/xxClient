using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BattleSpeedGlyph : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            for (int i = 0; i < 2; i++)
            {
                float x = r.xMin + r.width * (.05f + i * .48f);
                mesh.AddVert(new Vector3(x, r.yMin), color, Vector2.zero);
                mesh.AddVert(new Vector3(x, r.yMax), color, Vector2.zero);
                mesh.AddVert(new Vector3(x + r.width * .42f, r.center.y), color, Vector2.zero);
                mesh.AddTriangle(i * 3, i * 3 + 1, i * 3 + 2);
            }
        }
    }
}
