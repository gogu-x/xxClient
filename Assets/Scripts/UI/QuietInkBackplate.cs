using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>Resolution-independent paper tab and return arrow shared by page navigation.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class QuietInkBackplate : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect bounds=rectTransform.rect;
            var plate=new Rect(bounds.xMin+2,bounds.yMin+15,bounds.width-4,bounds.height-30);
            var shadow=plate;shadow.y-=2;
            Octagon(mesh,shadow,7,new Color(QuietInkUI.Ink.r,QuietInkUI.Ink.g,QuietInkUI.Ink.b,.08f));
            Octagon(mesh,plate,7,new Color(QuietInkUI.Gold.r,QuietInkUI.Gold.g,QuietInkUI.Gold.b,.78f));
            var inner=new Rect(plate.x+1.4f,plate.y+1.4f,plate.width-2.8f,plate.height-2.8f);
            Octagon(mesh,inner,6,new Color32(248,245,237,250));
            float left=bounds.xMin,center=plate.center.y;
            var tip=new Vector2(left+25,center);
            Stroke(mesh,tip,new Vector2(left+36,center+11),3,QuietInkUI.Jade);
            Stroke(mesh,tip,new Vector2(left+36,center-11),3,QuietInkUI.Jade);
            Stroke(mesh,tip,new Vector2(left+48,center),3,QuietInkUI.Jade);
        }

        private void Octagon(VertexHelper mesh,Rect rect,float cut,Color tint)
        {
            Vector2[] points={
                new Vector2(rect.xMin+cut,rect.yMin),new Vector2(rect.xMax-cut,rect.yMin),
                new Vector2(rect.xMax,rect.yMin+cut),new Vector2(rect.xMax,rect.yMax-cut),
                new Vector2(rect.xMax-cut,rect.yMax),new Vector2(rect.xMin+cut,rect.yMax),
                new Vector2(rect.xMin,rect.yMax-cut),new Vector2(rect.xMin,rect.yMin+cut)};
            int start=mesh.currentVertCount;
            Color32 vertexColor=tint*color;
            mesh.AddVert(rect.center,vertexColor,Vector2.zero);
            for(int i=0;i<points.Length;i++)mesh.AddVert(points[i],vertexColor,Vector2.zero);
            for(int i=0;i<points.Length;i++)mesh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
        }

        private void Stroke(VertexHelper mesh,Vector2 from,Vector2 to,float width,Color tint)
        {
            Vector2 direction=(to-from).normalized;
            Vector2 offset=new Vector2(-direction.y,direction.x)*width*.5f;
            int start=mesh.currentVertCount;
            Color32 vertexColor=tint*color;
            mesh.AddVert(from-offset,vertexColor,Vector2.zero);
            mesh.AddVert(from+offset,vertexColor,Vector2.zero);
            mesh.AddVert(to+offset,vertexColor,Vector2.zero);
            mesh.AddVert(to-offset,vertexColor,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2);
            mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
