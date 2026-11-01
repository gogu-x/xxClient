using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace XiuXian.Core
{
    /// <summary>
    /// 大厅建筑的运行时交互组件：根据绑定的 BuildingDef 显示贴图和常驻标签，
    /// 提供 hover/按下的缩放高光反馈，点击后把结果通过事件交给外部（LobbyBuildingController）
    /// 处理跳转/解锁提示，自身不直接依赖 UIManager，保持可复用、可单独测试。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BuildingHotspot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private Image glowImage;
        [SerializeField] private TMP_Text labelText;

        [Header("反馈参数")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float pressedScale = 0.96f;
        [SerializeField] private float scaleLerpSpeed = 12f;

        /// <summary>点击时触发，参数是自身绑定的配置，外部据此决定是否可跳转。</summary>
        public event Action<BuildingDef> OnBuildingClicked;

        public BuildingDef Def { get; private set; }

        private RectTransform _rect;
        private Coroutine _scaleRoutine;
        private bool _isPointerInside;
        private bool _isPressed;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
        }

        /// <summary>由 LobbyBuildingController 在生成后调用，把配置数据应用到显示上。</summary>
        public void Bind(BuildingDef def)
        {
            Def = def;
            if (def == null)
            {
                return;
            }

            _rect.anchorMin = new Vector2(0.5f, 0.5f);
            _rect.anchorMax = new Vector2(0.5f, 0.5f);
            _rect.anchoredPosition = def.anchoredPosition;
            _rect.sizeDelta = def.size;

            if (iconImage != null)
            {
                iconImage.sprite = def.icon;
            }
            if (glowImage != null)
            {
                glowImage.sprite = def.glowIcon != null ? def.glowIcon : def.icon;
                glowImage.gameObject.SetActive(false);
            }
            if (labelText != null)
            {
                labelText.text = def.displayName;
            }

            SetLocked(false);
        }

        /// <summary>解锁状态切换：锁定态灰化显示，点击只提示不跳转。</summary>
        public void SetLocked(bool locked)
        {
            var color = locked ? new Color(0.45f, 0.45f, 0.45f, 1f) : Color.white;
            if (iconImage != null)
            {
                iconImage.color = color;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isPointerInside = true;
            if (glowImage != null)
            {
                glowImage.gameObject.SetActive(true);
            }
            RestartScaleRoutine(hoverScale);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isPointerInside = false;
            if (!_isPressed && glowImage != null)
            {
                glowImage.gameObject.SetActive(false);
            }
            if (!_isPressed)
            {
                RestartScaleRoutine(1f);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            RestartScaleRoutine(pressedScale);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            RestartScaleRoutine(_isPointerInside ? hoverScale : 1f);
            if (!_isPointerInside && glowImage != null)
            {
                glowImage.gameObject.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnBuildingClicked?.Invoke(Def);
        }

        private void RestartScaleRoutine(float targetScale)
        {
            if (_scaleRoutine != null)
            {
                StopCoroutine(_scaleRoutine);
            }
            _scaleRoutine = StartCoroutine(LerpScale(targetScale));
        }

        private IEnumerator LerpScale(float targetScale)
        {
            var target = new Vector3(targetScale, targetScale, 1f);
            while (Vector3.Distance(_rect.localScale, target) > 0.001f)
            {
                _rect.localScale = Vector3.Lerp(_rect.localScale, target, Time.unscaledDeltaTime * scaleLerpSpeed);
                yield return null;
            }
            _rect.localScale = target;
        }
    }
}
