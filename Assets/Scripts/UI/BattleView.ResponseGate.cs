using System;
using System.Collections.Generic;

namespace XiuXian.Core
{
    public sealed partial class BattleView
    {
        private const int PendingBattleMessageLimit = 8192;
        private readonly Queue<Action> _pendingBattleMessages = new Queue<Action>();
        private bool _awaitingStartAck;
        private bool _startAckAccepted;
        private bool _battleResponseRejected;

        private void BeginBattleResponseGate()
        {
            _pendingBattleMessages.Clear();
            _awaitingStartAck = true;
            _startAckAccepted = false;
            _battleResponseRejected = false;
        }

        private void CloseBattleResponseGate()
        {
            _pendingBattleMessages.Clear();
            _awaitingStartAck = false;
            _startAckAccepted = false;
            _battleResponseRejected = true;
        }

        private bool BufferUntilStartAck(Action replay)
        {
            if (_battleResponseRejected) return true;
            if (_startAckAccepted) return false;
            if (!_awaitingStartAck) return true;
            if (_pendingBattleMessages.Count >= PendingBattleMessageLimit)
            {
                FailBattleResponse("待确认的战斗数据过多，请重新挑战", "同步失败");
                return true;
            }
            _pendingBattleMessages.Enqueue(replay);
            return true;
        }

        private void AcceptBattleResponse()
        {
            _awaitingStartAck = false;
            _startAckAccepted = true;
            while (_pendingBattleMessages.Count > 0 && !_battleResponseRejected)
                _pendingBattleMessages.Dequeue().Invoke();
        }

        private void FailBattleResponse(string message, string title)
        {
            CloseBattleResponseGate();
            CancelBattleCreateTimeout();
            _canBeginBattle = true;
            SetBattleStatus(message, title);
            ShowBattleRecoveryControl(returnToLogin: false);
            ClearFailedBattleCreation();
        }
    }
}
