using UnityEngine;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>Center-crops background UVs to fill a panel without stretching the artwork.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class CelestialBackgroundCover : BaseMeshEffect
    {
        public static void Apply(Image image)
        {
            if (image == null || image.sprite == null) return;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.useSpriteMesh = false;
            if (image.GetComponent<CelestialBackgroundCover>() == null)
                image.gameObject.AddComponent<CelestialBackgroundCover>();
            image.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper mesh)
        {
            if (!IsActive() || mesh.currentVertCount == 0) return;
            var image = graphic as Image;
            if (image == null || image.sprite == null) return;
            Rect rect = image.GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;
            float sourceAspect = image.sprite.rect.width / image.sprite.rect.height;
            float targetAspect = rect.width / rect.height;
            Vector2 fraction = targetAspect < sourceAspect
                ? new Vector2(targetAspect / sourceAspect, 1f)
                : new Vector2(1f, sourceAspect / targetAspect);
            UIVertex vertex = default;
            Vector2 min = new(float.MaxValue, float.MaxValue);
            Vector2 max = new(float.MinValue, float.MinValue);
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                min = Vector2.Min(min, vertex.uv0);
                max = Vector2.Max(max, vertex.uv0);
            }
            Vector2 center = (min + max) * 0.5f;
            for (int i = 0; i < mesh.currentVertCount; i++)
            {
                mesh.PopulateUIVertex(ref vertex, i);
                Vector2 uv = center + Vector2.Scale((Vector2)vertex.uv0 - center, fraction);
                vertex.uv0 = new Vector4(uv.x, uv.y, vertex.uv0.z, vertex.uv0.w);
                mesh.SetUIVertex(vertex, i);
            }
        }
    }
}
