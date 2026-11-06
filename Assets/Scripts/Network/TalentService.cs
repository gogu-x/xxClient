using System;
using Google.Protobuf;
using UnityEngine;

namespace XiuXian.Network
{
    /// <summary>服务器天赋快照与请求的唯一拥有者，页面关闭后继续接收操作结果。</summary>
    public sealed class TalentService : MonoBehaviour
    {
        private const float RequestTimeout = 12f;
        private static TalentService instance;
        private static RoleTalentData data;
        private static bool synchronized;
        private static string status = "天赋数据待同步";
        private NetworkManager net;
        private ulong pendingId;
        private string pendingAck;
        private float deadline;

        public static event Action Changed;
        public static RoleTalentData Data => data;
        public static bool HasData => data != null;
        public static bool IsPending => instance != null && instance.pendingId != 0;
        public static bool CanWrite => synchronized && HasData && !IsPending && instance != null && instance.net != null && instance.net.IsConnected;
        public static string Status => status;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            instance = null; data = null; synchronized = false; status = "天赋数据待同步"; Changed = null;
        }

        /// <summary>登录边界清除上一角色快照与在途请求，不会创建网络对象。</summary>
        public static void ClearSession()
        {
            data = null; synchronized = false; status = "天赋数据待同步";
            if (instance != null) instance.ClearPending();
            Changed?.Invoke();
        }

        public static bool RequestRefresh()
        {
            var service = Ensure();
            if (!service.CanSend(false)) return false;
            ulong requestId = NewRequestId();
            return service.Send(new GetRoleTalentReq { RequestId = requestId }, requestId,
                "GetRoleTalentAck", "正在同步天赋…");
        }

        public static bool Learn(int nodeId)
        {
            var service = Ensure();
            if (!service.CanSend(true)) return false;
            var node = FindNode(nodeId);
            if (node == null) { SetStatus(LoadoutErrors.ToUserMessage(ErrCode.InvalidTalentNode)); return false; }
            if (IsLearned(nodeId) || node.IsRoot) { SetStatus(LoadoutErrors.ToUserMessage(ErrCode.TalentAlreadyLearned)); return false; }
            if (node.ParentId >= 0 && !IsLearned(node.ParentId)) { SetStatus(LoadoutErrors.ToUserMessage(ErrCode.TalentPrerequisiteMissing)); return false; }
            if (data.AvailablePoints < node.PointCost) { SetStatus(LoadoutErrors.ToUserMessage(ErrCode.InsufficientTalentPoints)); return false; }
            ulong requestId = NewRequestId();
            return service.Send(new LearnRoleTalentReq
            {
                RequestId = requestId, ExpectedRevision = data.Revision, NodeId = nodeId
            }, requestId, "LearnRoleTalentAck", "正在修习天赋…");
        }

        public static bool Reset(int branch = -1)
        {
            var service = Ensure();
            if (!service.CanSend(true)) return false;
            bool available = branch == -1;
            foreach (var definition in data.Branches) if (definition.BranchId == branch) { available = true; break; }
            if (!available) { SetStatus(LoadoutErrors.ToUserMessage(ErrCode.InvalidTalentBranch)); return false; }
            ulong requestId = NewRequestId();
            return service.Send(new ResetRoleTalentReq
            {
                RequestId = requestId, ExpectedRevision = data.Revision, Branch = branch
            }, requestId, "ResetRoleTalentAck", "正在重置天赋…");
        }

        public static TalentNodeDefinition FindNode(int id)
        {
            if (data != null)
                foreach (var node in data.Nodes) if (node.NodeId == id) return node;
            return null;
        }

        public static bool IsLearned(int id) => data != null && data.LearnedNodeIds.Contains(id);

        private static TalentService Ensure()
        {
            if (instance != null) return instance;
            var network = NetworkManager.Instance;
            instance = network.GetComponent<TalentService>() ?? network.gameObject.AddComponent<TalentService>();
            return instance;
        }

        private static ulong NewRequestId()
        {
            ulong value = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
            return value == 0 ? 1UL : value;
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            net = GetComponent<NetworkManager>();
            net.RegisterProtoHandler("GetRoleTalentAck", GetRoleTalentAck.Parser, OnSnapshot);
            net.RegisterProtoHandler("LearnRoleTalentAck", LearnRoleTalentAck.Parser, OnLearned);
            net.RegisterProtoHandler("ResetRoleTalentAck", ResetRoleTalentAck.Parser, OnReset);
            net.OnDisconnected += Disconnected;
            net.OnError += Failed;
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (net != null)
            {
                net.UnregisterHandler("GetRoleTalentAck");
                net.UnregisterHandler("LearnRoleTalentAck");
                net.UnregisterHandler("ResetRoleTalentAck");
                net.OnDisconnected -= Disconnected;
                net.OnError -= Failed;
            }
            ClearSession();
            instance = null;
        }

        private void Update()
        {
            if (pendingId == 0 || Time.unscaledTime < deadline) return;
            ClearPending();
            data = null; synchronized = false;
            SetStatus("同步超时，请刷新确认天赋状态");
        }

        private bool CanSend(bool needsSnapshot)
        {
            if (pendingId != 0) return false;
            if (net == null || !net.IsConnected)
            {
                data = null; synchronized = false; SetStatus("连接已断开，请返回登录后重试"); return false;
            }
            if (needsSnapshot && (data == null || !synchronized))
            {
                SetStatus("请先刷新天赋数据"); return false;
            }
            return true;
        }

        private bool Send(IMessage message, ulong id, string response, string messageText)
        {
            pendingId = id; pendingAck = response; deadline = Time.unscaledTime + RequestTimeout;
            SetStatus(messageText);
            try { net.SendProto(message); return true; }
            catch (Exception)
            {
                ClearPending(); synchronized = false;
                SetStatus("天赋请求未能发送，请刷新重试");
                return false;
            }
        }

        private void OnSnapshot(GetRoleTalentAck ack) => Receive(ack.RequestId, "GetRoleTalentAck", ack.Data, ack.Code, "天赋已与服务器同步");
        private void OnLearned(LearnRoleTalentAck ack) => Receive(ack.RequestId, "LearnRoleTalentAck", ack.Data, ack.Code, "修习成功 · 下场战斗生效");
        private void OnReset(ResetRoleTalentAck ack) => Receive(ack.RequestId, "ResetRoleTalentAck", ack.Data, ack.Code, "天赋已重置 · 点数已返还");

        private void Receive(ulong id, string response, RoleTalentData snapshot, int code, string success)
        {
            // 不接收切换账号前、超时后或其他操作的迟到回包。
            if (pendingId == 0 || id != pendingId || response != pendingAck) return;
            ClearPending();
            synchronized = false;
            if (!LoadoutErrors.IsKnown(code))
            {
                SetStatus(LoadoutErrors.ToUserMessage(code));
                return;
            }
            if (snapshot == null)
            {
                SetStatus(code != ErrCode.Ok ? LoadoutErrors.ToUserMessage(code) : "服务器未返回天赋数据，请刷新重试");
                return;
            }
            if (string.IsNullOrEmpty(snapshot.RoleId) || (data != null &&
                (snapshot.RoleId != data.RoleId || snapshot.Revision < data.Revision)))
            {
                SetStatus("天赋数据校验失败，请刷新重试");
                return;
            }
            data = snapshot;
            synchronized = true;
            if (code != ErrCode.Ok) { SetStatus(LoadoutErrors.ToUserMessage(code)); return; }
            if (response != "GetRoleTalentAck") CombatStatsService.Invalidate();
            SetStatus(success);
        }

        private void Disconnected(string reason)
        {
            ClearPending(); data = null; synchronized = false; SetStatus("连接已断开，请返回登录后重试");
        }

        private void Failed(string reason)
        {
            if (pendingId == 0) return;
            ClearPending(); data = null; synchronized = false; SetStatus("天赋通信失败，请刷新确认操作结果");
        }

        private void ClearPending() { pendingId = 0; pendingAck = null; deadline = 0; }
        private static void SetStatus(string value) { status = value; Changed?.Invoke(); }
    }
}
