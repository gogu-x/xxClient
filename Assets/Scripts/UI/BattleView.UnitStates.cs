using System.Collections;
using UnityEngine;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private static readonly Color StunTint = QuietInkUI.Gold;
        private readonly Sprite[] _controlFxSprites = new Sprite[2];

        private void WarmupControlFx()
        {
            // 进入战斗时生成，两种纹理都不留到第一次控制命中再上传。
            for (int index = 0; index < _controlFxSprites.Length; index++)
            {
                if (_controlFxSprites[index] != null) continue;
                bool sigil = index == 1;
                // 白色几何星纹用颜色着色，避免蓝色技能图集乘金色后变成绿色。
                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = sigil ? "StunSigil" : "StunStar", filterMode = FilterMode.Bilinear };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f;
                    float dy = (y + .5f) / size * 2f - 1f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float contour = Mathf.Pow(Mathf.Abs(dx), .55f) + Mathf.Pow(Mathf.Abs(dy), .55f);
                    float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.94f, 1.05f, contour));
                    if (sigil)
                    {
                        float ring = Mathf.Min(Mathf.Abs(radius - .68f), Mathf.Abs(radius - .84f));
                        alpha = Mathf.Max(alpha, 1f - Mathf.SmoothStep(0f, 1f,
                            Mathf.InverseLerp(.008f, .028f, ring)));
                    }
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                texture.SetPixels(pixels);
                texture.Apply(false, true);
                _controlFxSprites[index] = Sprite.Create(texture, new Rect(0, 0, size, size),
                    new Vector2(.5f, .5f), size);
            }
        }

        private SpriteRenderer CreateControlFx(Transform parent, string name, bool sigil, float scale, int order)
        {
            int index = sigil ? 1 : 0;
            if (_controlFxSprites[index] == null) WarmupControlFx();
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _controlFxSprites[index];
            renderer.color = StunTint;
            renderer.sortingOrder = order;
            return renderer;
        }

        private void PrepareUnitControlVisuals(UnitVisual unit)
        {
            if (unit?.Sphere == null || unit.StunStars != null) return;
            // 随单位一起预建，状态切换只启用/隐藏，避免第一次眩晕时创建组件。
            unit.StunStars = new SpriteRenderer[3];
            for (int i = 0; i < unit.StunStars.Length; i++)
            {
                var star = CreateControlFx(unit.Sphere, "StunStar" + i, false, .085f, 102);
                star.enabled = false;
                unit.StunStars[i] = star;
            }
        }

        private void ReleaseControlFx()
        {
            for (int i = 0; i < _controlFxSprites.Length; i++)
            {
                var sprite = _controlFxSprites[i];
                _controlFxSprites[i] = null;
                if (sprite == null) continue;
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }

        private void SetUnitStunned(UnitVisual unit, bool stunned)
        {
            if (unit == null || unit.DeathStarted || unit.IsStunned == stunned) return;
            unit.IsStunned = stunned;
            // 命中类型来自真实配置；协议没有到期通知，后续施法才确认恢复，不猜倒计时。
            AppendReferenceLog(ReferenceUnitName(unit.UnitId) + (stunned
                ? "<color=#A17B35>眩晕</color>"
                : "<color=#347F78>恢复行动</color>"));
        }

        private IEnumerator AnimateStunCharge(UnitVisual caster, UnitVisual target, float duration, ulong action)
        {
            var root = new GameObject("StunCastCharge");
            root.transform.SetParent(_worldRoot.transform, false);
            _transientEffects.Add(root);
            var seal = CreateControlFx(root.transform, "GatheringSigil", true, .25f, 88);
            float elapsed = 0f;
            while (elapsed < duration && !IsActionAborted(action, caster.UnitId))
            {
                elapsed += PresentationDelta;
                float progress = Mathf.Clamp01(elapsed / duration);
                if (seal != null)
                {
                    seal.transform.position = GetBeamOrigin(caster, target);
                    seal.transform.localScale = Vector3.one * Mathf.Lerp(.22f, .65f, progress);
                    seal.transform.localRotation = Quaternion.Euler(0f, 0f, -80f * progress);
                    seal.color = new Color(StunTint.r, StunTint.g, StunTint.b,
                        Mathf.Clamp01(progress * 4f));
                }
                yield return null;
            }
            DestroyTransientEffect(root);
        }

        private void UpdateUnitControlVisuals()
        {
            foreach (var unit in _unitVisuals.Values)
            {
                bool visible = unit.IsStunned && !unit.DeathStarted && _resultPage == null;
                // 眩晕只阻止下一次行动，不能中断服务端已经允许、尚未结束的出手。
                bool completingCast = unit.AttackPlaying && PresentationTime < unit.AttackBlendEnd;
                float targetWeight = visible && !completingCast ? 1f : 0f;
                unit.StunPoseWeight = Mathf.MoveTowards(unit.StunPoseWeight, targetWeight,
                    PresentationDelta / .22f);
                float weight = Mathf.SmoothStep(0f, 1f, unit.StunPoseWeight);
                if (unit.ControlRoot != null)
                {
                    float phase = PresentationTime * 2.1f + Mathf.Abs(unit.UnitId.GetHashCode() % 628) * .01f;
                    float sway = Mathf.Sin(phase);
                    float floatOffset = Mathf.Sin(phase * .73f + .6f);
                    // 在原有受控倾斜上轻摆；逐帧绝对赋值，不累积偏移，也不改骨骼动画速度。
                    unit.ControlRoot.localPosition = new Vector3(sway * .012f,
                        -.10f + floatOffset * .008f, 0f) * weight;
                    unit.ControlRoot.localRotation = Quaternion.Euler(0f, 0f,
                        ((unit.IsDefender ? -9f : 9f) + sway * 1.5f) * weight);
                }
            }
        }

        private void UpdateUnitControlStars()
        {
            // 骨骼与飞行位置本帧更新完成后再定位，星环不滞后一帧。
            foreach (var unit in _unitVisuals.Values)
            {
                bool visible = unit.IsStunned && !unit.DeathStarted && _resultPage == null;
                if (unit.StunStars == null) continue;
                // 星环独立于受击节点与骨骼，死亡/离开时随单位一起清理，不占用战斗完成计数。
                Vector3 center = UnitHeadPosition(unit) + Vector3.up * .16f;
                center.z = -.65f;
                for (int i = 0; i < unit.StunStars.Length; i++)
                {
                    var star = unit.StunStars[i];
                    if (star == null) continue;
                    star.enabled = visible;
                    if (!visible) continue;
                    float angle = PresentationTime * 2.8f + i * Mathf.PI * 2f / unit.StunStars.Length;
                    star.transform.position = center + new Vector3(Mathf.Cos(angle) * .27f,
                        Mathf.Sin(angle) * .075f, 0f);
                    star.transform.rotation = Quaternion.Euler(0f, 0f, -PresentationTime * 50f);
                    star.color = StunTint;
                }
            }
        }

        private static void ClearUnitControlVisual(UnitVisual unit)
        {
            unit.IsStunned = false;
            unit.StunPoseWeight = 0f;
            if (unit.ControlRoot != null)
            {
                unit.ControlRoot.localPosition = Vector3.zero;
                unit.ControlRoot.localRotation = Quaternion.identity;
            }
            if (unit.StunStars != null)
                foreach (var star in unit.StunStars)
                    if (star != null) star.enabled = false;
        }

        private void RefreshUnitControlHud(UnitVisual unit, ReferenceUnitHud hud)
        {
            bool visible = unit.IsStunned && !unit.DeathStarted && _resultPage == null;
            if (hud.StunVisible != visible)
            {
                hud.StunVisible = visible;
                hud.StatusRoot.gameObject.SetActive(visible);
                foreach (var dot in hud.ControlDots) dot.gameObject.SetActive(visible);
            }
            if (!visible) return;
            // 持续旋转的控制标记与角色悬浮一起表明战斗仍在运行。
            for (int i = 0; i < hud.ControlDots.Length; i++)
            {
                float angle = PresentationTime * 2.5f + i * Mathf.PI * 2f / hud.ControlDots.Length;
                hud.ControlDots[i].anchoredPosition = new Vector2(90f + Mathf.Cos(angle) * 26f,
                    49f + Mathf.Sin(angle) * 6f);
            }
        }
    }
}
