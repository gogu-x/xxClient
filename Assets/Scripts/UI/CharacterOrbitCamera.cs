using UnityEngine;
using UnityEngine.EventSystems;

namespace XiuXian.Core
{
    /// <summary>
    /// 角色展示用的环绕摄像机：围绕一个目标点（角色模型中心）做水平/垂直旋转和距离缩放，
    /// 不使用 Unity 的全局鼠标输入（Input.GetMouseButton），而是通过 IDragHandler/IScrollHandler
    /// 接收拖拽和滚轮事件——这样可以直接挂在 UI 的 RawImage 上，只在鼠标位于该 UI 区域内时才响应，
    /// 不会跟场景里其他摄像机、其他 UI 的输入冲突（方案B：RenderTexture 嵌入 UI 的标准做法）。
    ///
    /// 使用方式：
    ///   1. 把这个组件挂在承载 RenderTexture 预览的 RawImage 节点上（不是挂在 Camera 上）。
    ///   2. 在 Inspector 或代码里把 targetCamera 指向实际渲染的 Camera，target 指向要环绕的物体（角色模型）。
    ///   3. OnDrag 时旋转 targetCamera 绕 target 的位置，OnScroll 时调整摄像机与 target 的距离。
    /// </summary>
    public class CharacterOrbitCamera : MonoBehaviour, IDragHandler, IScrollHandler, IBeginDragHandler
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform target;

        [Header("旋转")]
        [SerializeField] private float rotateSpeed = 0.25f;
        [SerializeField] private float minPitch = -30f;
        [SerializeField] private float maxPitch = 60f;

        [Header("缩放")]
        [SerializeField] private float zoomSpeed = 0.5f;
        [SerializeField] private float minDistance = 1.5f;
        [SerializeField] private float maxDistance = 6f;

        private float _yaw;
        private float _pitch = 10f;
        private float _distance;

        private void Awake()
        {
            if (targetCamera != null && target != null)
            {
                _distance = Vector3.Distance(targetCamera.transform.position, target.position);
            }
            else
            {
                _distance = (minDistance + maxDistance) * 0.5f;
            }
        }

        /// <summary>由 CharacterPreviewStage 在运行时创建摄像机/目标后调用，完成绑定并应用初始视角。</summary>
        public void Setup(Camera camera, Transform orbitTarget, float initialDistance)
        {
            targetCamera = camera;
            target = orbitTarget;
            _distance = Mathf.Clamp(initialDistance, minDistance, maxDistance);
            _yaw = 0f;
            _pitch = 10f;
            ApplyTransform();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            // 预留：未来可以在开始拖拽时暂停自动旋转展示等效果。
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (targetCamera == null || target == null)
            {
                return;
            }

            _yaw += eventData.delta.x * rotateSpeed;
            _pitch -= eventData.delta.y * rotateSpeed;
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            ApplyTransform();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (targetCamera == null || target == null)
            {
                return;
            }

            _distance -= eventData.scrollDelta.y * zoomSpeed;
            _distance = Mathf.Clamp(_distance, minDistance, maxDistance);
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var offset = rotation * new Vector3(0f, 0f, -_distance);
            targetCamera.transform.position = target.position + offset;
            targetCamera.transform.LookAt(target.position);
        }
    }
}
