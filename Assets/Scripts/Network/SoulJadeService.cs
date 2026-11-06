using System;
using Google.Protobuf;
using UnityEngine;

namespace XiuXian.Network
{
    /// <summary>魂玉网络请求的唯一持有者；库存、词条和镶嵌结果均以服务器快照为准。</summary>
    public sealed class SoulJadeService : MonoBehaviour
    {
        private const float RequestTimeout = 12f;
        private static SoulJadeService instance;
        private NetworkManager net;
        private ulong nextRequestId = (ulong)DateTime.UtcNow.Ticks;
        private ulong pendingId;
        private string pendingAck;
        private float deadline;
        private bool pendingMutation;

        public static SoulJadeService Instance
        {
            get
            {
                if (instance != null) return instance;
                var go = new GameObject("SoulJadeService");
                return go.AddComponent<SoulJadeService>();
            }
        }

        public event Action Changed;
        public event Action<bool, string> OperationCompleted;
        public RoleSoulJadeData Data { get; private set; }
        public bool IsBusy => pendingId != 0;
        public bool IsSynchronized { get; private set; }
        public bool CanMutate => IsSynchronized && Data != null && !IsBusy && net != null && net.IsConnected;
        public string Status { get; private set; } = "登录后同步魂玉";
        public int? LastErrorCode { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() { instance = null; }

        /// <summary>登录或切换账号前清除旧快照；不会为了清理而创建服务对象。</summary>
        public static void ClearSession()
        {
            if (instance != null) instance.ResetSession("登录后同步魂玉");
        }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
            net = NetworkManager.Instance;
            net.OnDisconnected += OnDisconnected;
            net.OnError += OnNetworkError;
            net.RegisterProtoHandler("GetRoleSoulJadeAck", GetRoleSoulJadeAck.Parser, OnGet);
            net.RegisterProtoHandler("ChangeRoleSoulJadeAck", ChangeRoleSoulJadeAck.Parser, OnChange);
            net.RegisterProtoHandler("UpgradeRoleSoulJadeAck", UpgradeRoleSoulJadeAck.Parser, OnUpgrade);
            net.RegisterProtoHandler("RefineRoleSoulJadeAck", RefineRoleSoulJadeAck.Parser, OnRefine);
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            if (net != null)
            {
                net.OnDisconnected -= OnDisconnected;
                net.OnError -= OnNetworkError;
                net.UnregisterHandler("GetRoleSoulJadeAck");
                net.UnregisterHandler("ChangeRoleSoulJadeAck");
                net.UnregisterHandler("UpgradeRoleSoulJadeAck");
                net.UnregisterHandler("RefineRoleSoulJadeAck");
            }
            instance = null;
        }

        public void Reload()
        {
            if (IsBusy) return;
            if (!Connected()) return;
            IsSynchronized = false;
            ulong id = NextId();
            Send(new GetRoleSoulJadeReq { RequestId = id }, id, "GetRoleSoulJadeAck", "正在同步魂玉…", false);
        }

        public bool Equip(int slot, string instanceId)
        {
            if (!RequireReady()) return false;
            bool available = false;
            foreach (var socket in Data.Slots) if (socket.Slot == slot) { available = true; break; }
            if (!available) return Reject(ErrCode.InvalidSlot);
            string target = instanceId ?? string.Empty;
            if (target.Length > 0)
            {
                var selected = FindItem(target);
                if (selected == null) return Reject(ErrCode.ItemNotOwned);
                foreach (var equipped in Data.Slots)
                {
                    if (equipped.Slot == slot || string.IsNullOrEmpty(equipped.InstanceId)) continue;
                    var item = FindItem(equipped.InstanceId);
                    if (item != null && item.ConfigId == selected.ConfigId)
                        return Reject(ErrCode.DuplicateJade);
                }
            }
            ulong id = NextId();
            return Send(new ChangeRoleSoulJadeReq
            {
                RequestId = id, ExpectedRevision = Data.Revision, Slot = slot, InstanceId = target
            }, id, "ChangeRoleSoulJadeAck", target.Length == 0 ? "正在卸下魂玉…" : "正在镶嵌魂玉…", true);
        }

        public bool Upgrade(string instanceId)
        {
            if (!RequireReady()) return false;
            var item = FindItem(instanceId);
            if (item == null) return Reject("请先选择一枚魂玉");
            if (item.Level >= item.MaxLevel) return Reject(ErrCode.MaxLevelReached);
            if (Data.Essence < item.UpgradeCost) return Reject(ErrCode.InsufficientEssence);
            ulong id = NextId();
            return Send(new UpgradeRoleSoulJadeReq
            {
                RequestId = id, ExpectedRevision = Data.Revision, InstanceId = instanceId
            }, id, "UpgradeRoleSoulJadeAck", "正在升级魂玉…", true);
        }

        public bool Refine(string instanceId)
        {
            if (!RequireReady()) return false;
            var item = FindItem(instanceId);
            if (item == null) return Reject("请先选择一枚魂玉");
            if (Data.Essence < item.RefineCost) return Reject(ErrCode.InsufficientEssence);
            ulong id = NextId();
            return Send(new RefineRoleSoulJadeReq
            {
                RequestId = id, ExpectedRevision = Data.Revision, InstanceId = instanceId
            }, id, "RefineRoleSoulJadeAck", "正在洗练魂玉…", true);
        }

        private SoulJadeItem FindItem(string id)
        {
            if (Data != null) foreach (var item in Data.Items) if (item.InstanceId == id) return item;
            return null;
        }

        private bool RequireReady()
        {
            if (IsBusy) return Reject("请求处理中，请稍候");
            if (!Connected()) return false;
            if (!IsSynchronized || Data == null) return Reject("请先刷新魂玉数据");
            return true;
        }

        private bool Connected()
        {
            if (net != null && net.IsConnected) return true;
            ResetSession("连接已断开，请重新登录");
            return false;
        }

        private bool Reject(string message)
        {
            LastErrorCode = null;
            Status = message;
            Changed?.Invoke();
            return false;
        }

        private bool Reject(int code)
        {
            LastErrorCode = code;
            Status = LoadoutErrors.ToUserMessage(code);
            Changed?.Invoke();
            return false;
        }

        private ulong NextId()
        {
            nextRequestId++;
            if (nextRequestId == 0) nextRequestId++;
            return nextRequestId;
        }

        private bool Send(IMessage request, ulong id, string ack, string status, bool mutation)
        {
            pendingId = id; pendingAck = ack; pendingMutation = mutation;
            LastErrorCode = null;
            deadline = Time.unscaledTime + RequestTimeout; Status = status;
            net.SendProto(request);
            Changed?.Invoke();
            return true;
        }

        private void OnGet(GetRoleSoulJadeAck ack) => Receive("GetRoleSoulJadeAck", ack.RequestId, ack.Data, ack.Code, "魂玉已与服务器同步");
        private void OnChange(ChangeRoleSoulJadeAck ack) => Receive("ChangeRoleSoulJadeAck", ack.RequestId, ack.Data, ack.Code, "镶嵌已更新 · 下场战斗生效");
        private void OnUpgrade(UpgradeRoleSoulJadeAck ack) => Receive("UpgradeRoleSoulJadeAck", ack.RequestId, ack.Data, ack.Code, "魂玉升级完成");
        private void OnRefine(RefineRoleSoulJadeAck ack) => Receive("RefineRoleSoulJadeAck", ack.RequestId, ack.Data, ack.Code, "魂玉洗练完成");

        private void Receive(string ack, ulong id, RoleSoulJadeData data, int code, string success)
        {
            // 会话清理、超时以及后续请求之后到达的旧响应不能覆盖当前角色。
            if (id == 0 || id != pendingId || ack != pendingAck) return;
            bool mutation = pendingMutation;
            ClearPending();
            LastErrorCode = code != ErrCode.Ok ? code : (int?)null;
            if (!LoadoutErrors.IsKnown(code))
            {
                IsSynchronized = false;
                Status = LoadoutErrors.ToUserMessage(code);
                Changed?.Invoke();
                if (mutation) OperationCompleted?.Invoke(false, Status);
                return;
            }
            bool accepted = data != null && !string.IsNullOrEmpty(data.RoleId) && (Data == null ||
                (Data.RoleId == data.RoleId && data.Revision >= Data.Revision));
            if (accepted) { Data = data; IsSynchronized = true; }
            else IsSynchronized = false;
            bool ok = code == ErrCode.Ok && accepted;
            if (ok && mutation) CombatStatsService.Invalidate();
            Status = ok ? success : code != ErrCode.Ok ? LoadoutErrors.ToUserMessage(code) : "魂玉数据未同步，请刷新后重试";
            Changed?.Invoke();
            if (mutation) OperationCompleted?.Invoke(ok, Status);
        }

        private void Update()
        {
            if (!IsBusy || Time.unscaledTime < deadline) return;
            bool mutation = pendingMutation;
            ClearPending(); IsSynchronized = false;
            Status = mutation ? "操作响应超时，正在确认魂玉结果…" : "魂玉同步超时，请点击刷新";
            Changed?.Invoke();
            // 只重新读取权威快照，不重发可能已经扣费的升级或洗练。
            if (mutation && net != null && net.IsConnected) Reload();
        }

        private void OnDisconnected(string reason) => ResetSession("连接已断开，请重新登录");
        private void OnNetworkError(string reason) => ResetSession("连接异常，请重新登录后同步魂玉");

        private void ResetSession(string status)
        {
            ClearPending(); Data = null; IsSynchronized = false; LastErrorCode = null; Status = status;
            Changed?.Invoke();
        }

        private void ClearPending()
        {
            pendingId = 0; pendingAck = null; pendingMutation = false; deadline = 0;
        }
    }
}
