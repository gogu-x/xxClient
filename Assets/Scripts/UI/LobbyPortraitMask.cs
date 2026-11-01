using UnityEngine;
using UnityEngine.UI;
namespace XiuXian.Core
{
    // Geometry-only stencil mask; no dependency on editor built-in sprites.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyPortraitMask : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r=rectTransform.rect;
            mesh.AddVert(r.center,color,new Vector2(.5f,.5f));
            const int segments=64;
            for(int i=0;i<segments;i++){
                float a=i*Mathf.PI*2f/segments;
                var uv=new Vector2(.5f+.5f*Mathf.Cos(a),.5f+.5f*Mathf.Sin(a));
                mesh.AddVert(new Vector2(r.xMin+uv.x*r.width,r.yMin+uv.y*r.height),color,uv);
            }
            for(int i=0;i<segments;i++)mesh.AddTriangle(0,i+1,(i+1)%segments+1);
        }
    }
}
