using UnityEngine;
using Q = XiuXian.Core.QuietInkUI;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private void WarmupInkCharacterAssets()
        {
            // Resolve the three independent foregrounds before combat starts.
            Q.Sprite("battle-player");
            Q.Sprite("battle-enemy-swordsman");
            Q.Sprite("battle-enemy-mage");
            GetInkFxSprite("fx-ring", 100);
            GetInkFxSprite("fx-slash", 101);
            GetInkFxSprite("fx-impact", 102);
            GetOrCreateBeamMaterial();
        }

        private Sprite GetInkFxSprite(string id, int key)
        {
            if (_skillFxSprites.TryGetValue(key, out var cached) && cached != null) return cached;
            var source = Q.Sprite(id);
            if (source == null) return null;
            // Effect motion is authored in world units; normalize imported sprite pixels to height one.
            var sprite = Sprite.Create(source.texture, source.rect, new Vector2(.5f, .5f),
                Mathf.Max(1, source.rect.height), 0, SpriteMeshType.FullRect);
            sprite.name = id + "-world";
            _skillFxSprites[key] = sprite;
            return sprite;
        }

        private UnitVisual CreateInkCharacterVisual(string unitId, bool defender)
        {
            var motion = new GameObject("InkUnit-" + unitId).transform;
            motion.SetParent(_worldRoot.transform, false);
            var body = new GameObject("Body").transform;
            body.SetParent(motion, false);
            var illustration = body.gameObject.AddComponent<SpriteRenderer>();
            illustration.sortingOrder = defender ? 35 : 45;
            illustration.color = Color.white;
            var unit = new UnitVisual
            {
                UnitId = unitId,
                IsDefender = defender,
                Sphere = motion,
                Body = body,
                Renderer = illustration,
                Illustration = illustration,
                BaseColor = Color.white,
                BodyBaseLocalRotation = Quaternion.identity
            };
            UpdateInkCharacterSprite(unit);
            return unit;
        }

        private void UpdateInkCharacterSprite(UnitVisual unit)
        {
            if (unit?.Illustration == null) return;
            int position = unit.IsDefender ? _defenders.IndexOf(unit) : _attackers.IndexOf(unit);
            bool mage = position >= 0 ? position % 3 == 0 : (unit.UnitId.GetHashCode() & 1) == 0;
            string id = !unit.IsDefender ? "battle-player" :
                mage ? "battle-enemy-mage" : "battle-enemy-swordsman";
            var sprite = Q.Sprite(id) ?? Q.Sprite(unit.IsDefender ? "hero-character" : "hero-login") ??
                Q.Sprite("hero-login") ?? Q.Sprite("character");
            if (sprite == null) return;
            unit.Illustration.sprite = sprite;
            unit.Illustration.flipX = unit.IsDefender && !mage && position % 3 == 2;
            unit.Illustration.sortingOrder = (unit.IsDefender ? 35 : 45) - Mathf.Max(0, position / 3);
            var bounds = sprite.bounds;
            float scale = CharacterNormalizedHeight / Mathf.Max(.001f, bounds.size.y);
            unit.BodyBaseLocalScale = Vector3.one * scale;
            unit.BodyBaseLocalPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, 0);
            unit.BodyBaseLocalRotation = Quaternion.identity;
            if (!unit.DeathStarted)
            {
                unit.Body.localPosition = unit.BodyBaseLocalPosition;
                unit.Body.localScale = unit.BodyBaseLocalScale;
                unit.Body.localRotation = Quaternion.identity;
            }
        }

        private void PlayCharacterAttack(UnitVisual unit, float secondsUntilRelease, float endTime)
        {
            if (unit == null || unit.DeathStarted) return;
            unit.AttackBlendStart = PresentationTime;
            unit.AttackReleaseAt = PresentationTime + Mathf.Max(0, secondsUntilRelease);
            unit.AttackBlendEnd = endTime;
            unit.AttackPlaying = true;
        }

        private void FinishCharacterAttack(UnitVisual unit)
        {
            if (unit != null) unit.AttackBlendEnd = Mathf.Min(unit.AttackBlendEnd, PresentationTime);
        }

        private void UpdateCharacterAnimations()
        {
            foreach (var unit in _unitVisuals.Values)
            {
                if (unit.CastRoot == null || unit.DeathStarted || !unit.Sphere.gameObject.activeInHierarchy) continue;
                float phase = PresentationTime * 1.7f + Mathf.Abs(unit.UnitId.GetHashCode() % 628) * .01f;
                float charge = 0f, release = 0f;
                if (unit.AttackPlaying)
                {
                    float windup = Mathf.Max(.01f, unit.AttackReleaseAt - unit.AttackBlendStart);
                    charge = Mathf.SmoothStep(0, 1, Mathf.Clamp01((PresentationTime - unit.AttackBlendStart) / windup));
                    float released = PresentationTime - unit.AttackReleaseAt;
                    if (released >= 0f) release = Mathf.Sin(Mathf.Clamp01(released / .18f) * Mathf.PI);
                    float settle = 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((PresentationTime - unit.AttackBlendEnd) / .14f));
                    charge *= settle;
                    release *= settle;
                    if (settle <= 0f) unit.AttackPlaying = false;
                }
                float direction = unit.IsDefender ? -1f : 1f;
                float idle = Mathf.Lerp(1f, .45f, unit.StunPoseWeight);
                // Only a small local casting accent; flight stays inside this unit's formation slot.
                unit.CastRoot.localPosition = new Vector3(Mathf.Sin(phase) * .009f * idle,
                    Mathf.Cos(phase * .82f) * .012f * idle + direction * (.025f * charge + .045f * release), 0f);
                unit.CastRoot.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Sin(phase * .75f) * .8f * idle + direction * (-3.5f * charge + 6f * release));
            }
        }

        private void StopCharacterAttack(UnitVisual unit)
        {
            if (unit == null) return;
            unit.AttackPlaying = false;
            if (unit.CastRoot != null)
            {
                unit.CastRoot.localPosition = Vector3.zero;
                unit.CastRoot.localRotation = Quaternion.identity;
            }
        }

        private static Vector3 UnitHeadPosition(UnitVisual unit)
        {
            if (unit?.Illustration != null)
            {
                var bounds = unit.Illustration.bounds;
                return new Vector3(bounds.center.x, bounds.max.y + .12f, -.65f);
            }
            return unit.Sphere.position + Vector3.up * CharacterNormalizedHeight * unit.Sphere.localScale.y;
        }

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            // SpriteRenderer.color preserves the shared unlit material and its alpha blend.
            if (renderer is SpriteRenderer sprite) sprite.color = color;
        }
    }
}
