using System;
using UnityEngine;

namespace XiuXian.Network
{
    /// <summary>Displays the server's next-battle attributes without calculating them locally.</summary>
    public sealed class CombatStatsService : MonoBehaviour
    {
        private const float RequestTimeout = 12f;
        private static CombatStatsService instance;
        private static RoleCombatStatsData data;
        private static string roleId;
        private static string status = "角色属性待同步";
        private NetworkManager net;
        private ulong pendingId;
        private float deadline;

        public static event Action Changed;
        public static event Action Invalidated;
        public static RoleCombatStatsData Data => data;
        public static bool IsPending => instance != null && instance.pendingId != 0;
        public static string Status => status;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime()
        {
            instance = null; data = null; roleId = null; status = "角色属性待同步";
            Changed = null; Invalidated = null;
        }

        /// <summary>Clears account state without creating a service or sending a request.</summary>
        public static void ClearSession()
        {
            if (instance != null) instance.ClearPending();
            data = null; roleId = null;
            SetStatus("角色属性待同步");
        }

        /// <summary>A successful loadout change also invalidates any earlier in-flight query.</summary>
        public static void Invalidate()
        {
            if (instance != null) instance.ClearPending();
            data = null;
            SetStatus("养成已更新，属性待同步");
            // Only a visible consumer requests another snapshot. Hidden pages stay quiet.
            Invalidated?.Invoke();
        }

        public static bool RequestRefresh()
        {
            if (IsPending) return false;
            var service = Ensure();
            data = null;
            if (service.net == null || !service.net.IsConnected)
            {
                SetStatus("连接已断开，请返回登录后重试");
                return false;
            }
            ulong id = BitConverter.ToUInt64(Guid.NewGuid().ToByteArray(), 0);
            if (id == 0) id = 1;
            service.pendingId = id;
            service.deadline = Time.unscaledTime + RequestTimeout;
            SetStatus("正在同步角色属性…");
            try
            {
                service.net.SendProto(new GetRoleCombatStatsReq { RequestId = id });
                return true;
            }
            catch (Exception)
            {
                service.ClearPending();
                SetStatus("属性请求未能发送，请重试");
                return false;
            }
        }

        private static CombatStatsService Ensure()
        {
            if (instance == null)
            {
                var network = NetworkManager.Instance;
                instance = network.GetComponent<CombatStatsService>() ?? network.gameObject.AddComponent<CombatStatsService>();
            }
            instance.RegisterHandlers();
            return instance;
        }

        private void Awake() { RegisterHandlers(); }
        private void OnEnable() { RegisterHandlers(); }

        private void RegisterHandlers()
        {
            if (instance != null && instance != this) { Destroy(this); return; }
            instance = this;
            net = GetComponent<NetworkManager>();
            if (net == null) return;
            net.RegisterProtoHandler("GetRoleCombatStatsAck", GetRoleCombatStatsAck.Parser, OnSnapshot);
            // Rebinding is safe after script reload and never duplicates connection callbacks.
            net.OnDisconnected -= Disconnected;
            net.OnError -= Failed;
            net.OnDisconnected += Disconnected;
            net.OnError += Failed;
        }

        private void OnDisable()
        {
            if (instance != this) return;
            if (net != null)
            {
                net.UnregisterHandler("GetRoleCombatStatsAck");
                net.OnDisconnected -= Disconnected;
                net.OnError -= Failed;
            }
            ClearSession();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            ClearSession();
            instance = null;
        }

        private void Update()
        {
            if (pendingId == 0 || Time.unscaledTime < deadline) return;
            ClearPending(); data = null;
            SetStatus("属性同步超时，请点击刷新重试");
        }

        private void OnSnapshot(GetRoleCombatStatsAck ack)
        {
            if (pendingId == 0 || ack.RequestId != pendingId) return;
            ClearPending(); data = null;
            if (!ack.HasCode || ack.Code != ErrCode.Ok)
            {
                SetStatus(LoadoutErrors.ResponseMessage(ack.HasCode, ack.Code));
                return;
            }
            var snapshot = ack.Data;
            if (snapshot == null || string.IsNullOrEmpty(snapshot.RoleId) ||
                (!string.IsNullOrEmpty(roleId) && roleId != snapshot.RoleId))
            {
                SetStatus("角色属性校验失败，请重新登录后重试");
                return;
            }
            roleId = snapshot.RoleId;
            data = snapshot;
            SetStatus("当前养成属性 · 下场战斗生效");
        }

        private void Disconnected(string reason)
        {
            ClearPending(); data = null; roleId = null;
            SetStatus("连接已断开，请返回登录后重试");
        }

        private void Failed(string reason)
        {
            ClearPending(); data = null;
            SetStatus("属性通信失败，请刷新重试");
        }

        private void ClearPending() { pendingId = 0; deadline = 0; }
        private static void SetStatus(string value) { status = value; Changed?.Invoke(); }
    }
}
