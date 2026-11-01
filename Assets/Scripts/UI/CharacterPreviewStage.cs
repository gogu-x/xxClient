using UnityEngine;

namespace XiuXian.Core
{
    public class CharacterPreviewStage : MonoBehaviour
    {
        [SerializeField] private Camera previewCamera;
        [SerializeField] private Transform modelRoot;
        [SerializeField] private RenderTexture renderTexture;
        [SerializeField] private CharacterOrbitCamera orbitCamera;
        [SerializeField] private float initialDistance = 3f;

        private bool _displayModelReady;

        public RenderTexture RenderTexture => renderTexture;
        public CharacterOrbitCamera OrbitCamera => orbitCamera;
        public Camera PreviewCamera => previewCamera;
        public Transform ModelRoot => modelRoot;
        public float InitialDistance => initialDistance;

        public void SetActiveStage(bool active)
        {
            if (previewCamera != null) previewCamera.enabled = active;
        }

        public void EnsureDisplayModel()
        {
            if (_displayModelReady || modelRoot == null) return;
            var prefab = Resources.Load<GameObject>("Characters/Vampire A Lusth@Double Dagger Stab");
            if (prefab == null) return;

            for (int i = modelRoot.childCount - 1; i >= 0; i--)
                Destroy(modelRoot.GetChild(i).gameObject);

            var model = Instantiate(prefab, modelRoot);
            model.name = "WardrobeCharacter";
            model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SetLayerRecursively(model, modelRoot.gameObject.layer);

            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                float scale = 2.25f / Mathf.Max(0.01f, bounds.size.y);
                model.transform.localScale = Vector3.one * scale;
                bounds = CalculateBounds(renderers);
                model.transform.position += modelRoot.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }

            _displayModelReady = true;
            if (orbitCamera != null && previewCamera != null)
                orbitCamera.Setup(previewCamera, modelRoot, initialDistance);
        }

        public void ApplySkin(Color primary, Color accent)
        {
            EnsureDisplayModel();
            if (modelRoot == null) return;
            foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.materials;
                foreach (var material in materials)
                {
                    Color tint = Color.Lerp(Color.white, primary, 0.42f);
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", tint);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", accent * 0.38f);
                    }
                }
            }
        }

        public void SetModel(GameObject modelInstance)
        {
            if (modelRoot == null) return;
            for (int i = modelRoot.childCount - 1; i >= 0; i--)
                Destroy(modelRoot.GetChild(i).gameObject);
            if (modelInstance != null)
            {
                modelInstance.transform.SetParent(modelRoot, false);
                modelInstance.transform.localPosition = Vector3.zero;
                modelInstance.transform.localRotation = Quaternion.identity;
                SetLayerRecursively(modelInstance, modelRoot.gameObject.layer);
            }
            _displayModelReady = modelInstance != null;
            if (orbitCamera != null && previewCamera != null)
                orbitCamera.Setup(previewCamera, modelRoot, initialDistance);
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
