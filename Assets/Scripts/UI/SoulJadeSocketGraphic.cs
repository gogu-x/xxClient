using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    // UI mesh follows the socket silhouette, so neighbouring ring artwork is excluded.
    // Uses UVs only; textures stay non-readable and no PNG is duplicated or modified.
    public sealed class SoulJadeSocketGraphic : Image
    {
        private static readonly Vector2[] Contour = {
            new Vector2(374,112),new Vector2(397,145),new Vector2(461,165),
            new Vector2(498,209),new Vector2(516,264),new Vector2(492,332),
            new Vector2(447,370),new Vector2(395,391),new Vector2(374,409),
            new Vector2(348,391),new Vector2(289,370),new Vector2(251,330),
            new Vector2(234,268),new Vector2(251,206),new Vector2(289,163),new Vector2(350,143)
        };

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (sprite == null) return;
            Add(mesh, new Vector2(374,270));
            for(int i=0;i<Contour.Length;i++) Add(mesh,Contour[i]);
            for(int i=0;i<Contour.Length;i++) mesh.AddTriangle(0,i+1,(i+1)%Contour.Length+1);
        }

        private void Add(VertexHelper mesh, Vector2 point)
        {
            Rect rect=GetPixelAdjustedRect();
            float u=(point.x-233)/284, v=1-(point.y-110)/301;
            Rect tex=sprite.rect;
            Vector2 uv=new Vector2((tex.x+u*tex.width)/sprite.texture.width,(tex.y+v*tex.height)/sprite.texture.height);
            mesh.AddVert(new Vector3(rect.x+u*rect.width,rect.y+v*rect.height),color,uv);
        }
    }
}
