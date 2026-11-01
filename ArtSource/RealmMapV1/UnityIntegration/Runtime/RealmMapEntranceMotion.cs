using UnityEngine;

namespace XiuXian.RealmMap
{
    /// <summary>One gentle entrance-art response; hit targets and labels remain stationary.</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class RealmMapEntranceMotion : MonoBehaviour
    {
        public const float Duration = .65f;
        public bool IsPlaying => playing;
        private RectTransform rect;
        private Vector2 restPosition;
        private Vector3 restScale;
        private float startedAt, lift;
        private bool initialized, playing;

        private void Awake()
        {
            CaptureRestPose();
            enabled = false;
        }

        private void CaptureRestPose()
        {
            if (initialized) return;
            rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition;
            restScale = rect.localScale;
            initialized = true;
        }

        public void Play(float liftDistance)
        {
            CaptureRestPose();
            RestorePose();
            lift = Mathf.Max(0, liftDistance);
            startedAt = Time.unscaledTime;
            playing = true;
            enabled = true;
        }

        public void Stop()
        {
            playing = false;
            RestorePose();
            enabled = false;
        }

        private void Update()
        {
            if (!playing) return;
            float t = Mathf.Clamp01((Time.unscaledTime - startedAt) / Duration);
            if (t >= 1) { Stop(); return; }
            float pulse = Mathf.Sin(t * Mathf.PI);
            pulse *= pulse;
            rect.anchoredPosition = restPosition + Vector2.up * (lift * pulse);
            rect.localScale = restScale * (1 + .018f * pulse);
        }

        private void RestorePose()
        {
            if (!initialized || rect == null) return;
            rect.anchoredPosition = restPosition;
            rect.localScale = restScale;
        }

        private void OnDisable()
        {
            playing = false;
            RestorePose();
        }
    }
}
