using UnityEngine;
using XiuXian.Network;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private NetworkManager _battleConnection;
        private GameObject _battleRecoveryControl;

        private void SubscribeBattleConnection(NetworkManager net)
        {
            UnsubscribeBattleConnection();
            _battleConnection = net;
            net.OnDisconnected += OnBattleConnectionLost;
            net.OnError += OnBattleConnectionError;
            ClearBattleRecoveryControl();
        }

        private void UnsubscribeBattleConnection()
        {
            if (_battleConnection == null) return;
            _battleConnection.OnDisconnected -= OnBattleConnectionLost;
            _battleConnection.OnError -= OnBattleConnectionError;
            _battleConnection = null;
        }

        private void OnBattleConnectionError(string error)
        {
            // 仅实际失去连接时中止；连接仍有效的错误不丢弃已收到的战报。
            if (_battleConnection != null && !_battleConnection.IsConnected)
                OnBattleConnectionLost(error);
        }

        private void OnBattleConnectionLost(string reason)
        {
            // Finished 已收齐则只剩本地播放，不因随后断线丢掉完整结算。
            if (!isActiveAndEnabled || !_registered || _timelinePlanner.IsSealed) return;
            if (_battleCreateTimeout == null && string.IsNullOrEmpty(_battleId) && _playRoutine == null) return;

            UnregisterNetworkHandlers();
            StopAllCoroutines();
            _battleCreateTimeout = null;
            _playRoutine = null;
            _runningAnimations = 0;
            _events.Clear();
            _motionQueue.Clear();
            _reactionQueues.Clear();
            _reactionActive.Clear();
            ClearTransientEffects();

            foreach (var visual in _unitVisuals.Values)
            {
                visual.DeathRoutine = null;
                visual.CastPoseUntil = PresentationTime;
                ClearUnitControlVisual(visual);
                if (visual.DeathStarted)
                {
                    if (visual.Sphere != null) visual.Sphere.gameObject.SetActive(false);
                    continue;
                }
                // 保留待机图和飞行更新，避免网络中断时冻结在出手/受击姿势。
                FinishCharacterAttack(visual);
                SetRendererColor(visual.Renderer, visual.BaseColor);
                if (visual.ReactionRoot != null)
                {
                    visual.ReactionRoot.localPosition = Vector3.zero;
                    visual.ReactionRoot.localRotation = Quaternion.identity;
                }
            }
            _canBeginBattle = false;
            ShowBattleRecoveryControl(returnToLogin: true);
            _referenceLog.Clear();
            SetBattleStatus("连接已中断\n请返回登录后重试", "连接中断");
        }

        private void ReturnToBattleLogin()
        {
            UIManager.Instance.Show(UIScreen.Home);
        }

        private void ShowBattleRecoveryControl(bool returnToLogin)
        {
            if (_referenceDesign == null) return;
            ClearBattleRecoveryControl();
            // These controls exist only for an error, never during normal playback.
            _battleRecoveryControl = ReferenceControl(_referenceDesign,
                returnToLogin ? "BattleReturnToLogin" : "BattleRetry",
                returnToLogin ? "返回登录" : "重试", 598, 1586, 289, 76,
                returnToLogin ? ReturnToBattleLogin : BeginBattle, 30).gameObject;
            LayoutBattleRecoveryControl();
        }

        private void ClearBattleRecoveryControl()
        {
            if (_battleRecoveryControl != null)
            {
                _battleRecoveryControl.SetActive(false);
                Destroy(_battleRecoveryControl);
                _battleRecoveryControl = null;
            }
            LayoutBattleRecoveryControl();
        }
    }
}
