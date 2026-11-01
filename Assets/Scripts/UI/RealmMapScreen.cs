using UnityEngine;
using XiuXian.RealmMap;

namespace XiuXian.Core
{
    /// <summary>Connects the current trial to Qingyun without inventing separate server realm progress.</summary>
    [RequireComponent(typeof(RealmMapPanel))]
    public sealed class RealmMapScreen : MonoBehaviour
    {
        private const string CurrentTrialEntrance = "qingyun";
        [SerializeField] private RealmMapPanel panel;
        public RealmMapPanel Panel => panel;

        public static RealmMapScreen Show(Transform context = null)
        {
            var manager = UIManager.Instance;
            if (manager == null) return null;
            var canvas = context != null ? context.GetComponentInParent<Canvas>() : null;
            if (canvas == null)
            {
                var main = FindAnyObjectByType<MainPanel>(FindObjectsInactive.Include);
                if (main != null) canvas = main.GetComponentInParent<Canvas>();
            }
            if (canvas == null)
            {
                Debug.LogError("[RealmMapScreen] 无法打开秘境：入口不在 Canvas 下。");
                return null;
            }
            var screen = FindAnyObjectByType<RealmMapScreen>(FindObjectsInactive.Include);
            if (screen == null)
            {
                var view = RealmMapPanel.Show(canvas.transform);
                view.name = "RealmMapView";
                screen = view.gameObject.AddComponent<RealmMapScreen>();
            }
            else if (screen.transform.parent != canvas.transform)
                screen.transform.SetParent(canvas.transform, false);
            screen.BindActions();
            manager.RegisterScreen(UIScreen.Realm, screen.gameObject);
            manager.Show(UIScreen.Realm);
            screen.panel.Open();
            return screen;
        }

        private void OnEnable() { BindActions(); }

        private void BindActions()
        {
            if (panel == null) panel = GetComponent<RealmMapPanel>();
            panel.EntryRequested -= EnterRealm;
            panel.EntryRequested += EnterRealm;
            panel.Closed -= ReturnToLobby;
            panel.Closed += ReturnToLobby;
            // Qingyun is the visual entrance to the existing current-trial action.
            // The server still chooses the player's tower layer; no 24-level mapping is implied.
            panel.SetEntryAvailability(CurrentTrialEntrance, true, "当前试炼已开放");
            var design = transform.Find("RealmMapDesign");
            var existing = design == null ? null : design.Find("CurrentTrial");
            if (existing != null)
            {
                existing.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (panel == null) return;
            panel.EntryRequested -= EnterRealm;
            panel.Closed -= ReturnToLobby;
        }

        private void EnterRealm(RealmMapNode node)
        {
            if (node != null && node.id == CurrentTrialEntrance && panel.IsEntryAvailable(node.id))
                BattleView.Show(transform);
        }

        private void ReturnToLobby()
        {
            if (UIManager.Instance != null) UIManager.Instance.Show(UIScreen.Main);
        }
    }
}
