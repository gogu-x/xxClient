using UnityEngine;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private CanvasGroup _sessionVisibility;
        private bool _hasBattleSession;
        private bool _isBattleMinimized;
        private bool _battleIsFinished;
        private int _startBattleRequestCount;

        public static bool HasResumableBattle => _instance != null && _instance._hasBattleSession;
        public static bool IsBattleMinimized => HasResumableBattle && _instance._isBattleMinimized;
        public static bool BattleIsFinished => HasResumableBattle && _instance._battleIsFinished;
        public static string CurrentBattleId => _instance == null ? string.Empty : _instance._battleId;
        public static int StartBattleRequestCount => _instance == null ? 0 : _instance._startBattleRequestCount;

        // One-shot feedback has no gameplay state. Discard it while hidden instead
        // of creating uninitialized TMP objects or replaying old hits on resume.
        private bool CanShowTransientFeedback => !_isBattleMinimized &&
            _worldRoot != null && _worldRoot.activeInHierarchy;

        /// <summary>Return to the existing presentation without creating another server battle.</summary>
        public static bool ResumeExisting()
        {
            if (!HasResumableBattle || UIManager.Instance == null) return false;
            UIManager.Instance.RegisterScreen(UIScreen.Battle, _instance.gameObject);
            UIManager.Instance.Show(UIScreen.Battle);
            _instance.transform.SetAsLastSibling();
            return true;
        }

        public static void MinimizeToLobby()
        {
            if (_instance == null || UIManager.Instance == null) return;
            UIManager.Instance.Show(UIScreen.Main);
        }

        /// <summary>
        /// Called by UIManager. Keep this component active so its network handlers,
        /// presentation clock and tracked coroutines continue while the UI is hidden.
        /// BattleWorld has no coroutine owners; disabling it hides both cameras and
        /// renderers without stopping the playback running on this root component.
        /// </summary>
        internal void SetScreenVisible(bool visible)
        {
            if (visible && !gameObject.activeSelf) gameObject.SetActive(true);
            _isBattleMinimized = !visible && _hasBattleSession;
            if (_sessionVisibility == null)
            {
                _sessionVisibility = GetComponent<CanvasGroup>();
                // Unity may return a destroyed/native-null component wrapper, which
                // is not a CLR null and therefore must not use the ?? operator.
                if (_sessionVisibility == null)
                    _sessionVisibility = gameObject.AddComponent<CanvasGroup>();
            }
            _sessionVisibility.alpha = visible ? 1 : 0;
            _sessionVisibility.interactable = visible;
            _sessionVisibility.blocksRaycasts = visible;
            if (_worldRoot != null) _worldRoot.SetActive(visible);
            if (!visible && !_hasBattleSession) gameObject.SetActive(false);
        }

        /// <summary>Leaving the account or dismissing a result ends the local session.</summary>
        internal void CloseBattleSession()
        {
            _hasBattleSession = false;
            _isBattleMinimized = false;
            _battleIsFinished = false;
            _pendingFinished = null;
            _battleId = string.Empty;
            _events.Clear();
            _cancelledActions.Clear();
            _cancelledActionTargets.Clear();
            _timelinePlanner = new BattleTimelinePlanner();
            SetScreenVisible(false); // OnDisable owns coroutine, handler and effect cleanup.
        }

        private void ClearFailedBattleCreation()
        {
            bool wasMinimized = _isBattleMinimized;
            _hasBattleSession = false;
            _isBattleMinimized = false;
            _battleIsFinished = false;
            _battleId = string.Empty;
            // Keep a visible error page available for retry. A hidden failed
            // request has no battle to resume, so release its normal lifecycle.
            if (wasMinimized) SetScreenVisible(false);
        }

        private void ReturnToLobby()
        {
            // A result already visible to the player is acknowledged by its return
            // button. A result produced while minimized stays available on the bubble.
            if (_battleIsFinished) CloseBattleSession();
            MinimizeToLobby();
        }
    }
}
