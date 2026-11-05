using System.Collections;
using UnityEngine;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private IEnumerator AnimateChargedEffect(UnitVisual visual, bool ice)
        {
            if (!CanShowTransientFeedback || visual?.Sphere == null || _worldRoot == null)
                yield break;

            var root = new GameObject(ice ? "IceBurstEffect" : "ThunderEffect");
            root.transform.SetParent(_worldRoot.transform, false);
            _transientEffects.Add(root);
            var ring = CreateFxRenderer(root.transform, "ElementRing", 0, 1, 0.1f, 94);
            var impact = CreateFxRenderer(root.transform, "ElementImpact", ice ? 2 : 1, 0, 0.1f, 98);
            Color tint = ice ? new Color(0.36f, 0.86f, 1f) : new Color(1f, 0.82f, 0.24f);
            const float duration = 0.48f;
            float elapsed = 0f;
            while (elapsed < duration && CanShowTransientFeedback)
            {
                elapsed += PresentationDelta;
                float t = Mathf.Clamp01(elapsed / duration);
                var position = GetBeamTarget(visual);
                position.z = -0.62f;
                root.transform.position = position;
                AnimateImpactSprite(ring, t, 0.12f, ice ? 0.95f : 0.65f, 1f - t);
                AnimateImpactSprite(impact, t, 0.18f, ice ? 0.75f : 1.05f, 1f - t);
                if (ring != null) ring.color = new Color(tint.r, tint.g, tint.b, (1f - t) * 0.8f);
                if (impact != null)
                {
                    impact.color = new Color(tint.r, tint.g, tint.b, 1f - t);
                    if (!ice) impact.transform.localScale = new Vector3(0.32f, 1.2f, 1f) * Mathf.Lerp(0.5f, 1f, t);
                }
                yield return null;
            }
            DestroyTransientEffect(root);
        }
    }
}
