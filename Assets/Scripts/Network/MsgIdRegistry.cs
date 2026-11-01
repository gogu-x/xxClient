using System.Collections.Generic;

namespace XiuXian.Network
{
    /// <summary>
    /// 复现后端 gogs/codec.ProtoCodec 的消息 ID 算法：
    ///   msgID = uint16(FNV32a(GoPkgPath + "." + MessageName))
    /// 见 gogs/codec/proto.go 中的 msgID() 函数。
    ///
    /// 前后端必须对同一个消息类型算出相同的 ID，因此这里需要维护
    /// 一份「消息名 -> 完整 Go 包路径」的映射，与 protocol/*.proto 的
    /// go_package 保持一致。
    /// </summary>
    public static class MsgIdRegistry
    {
        // Go 包路径必须与当前 protocol/cspb/*.proto 的 option go_package 完全一致。
        private const string PkgGateway = "github.com/gogu-x/gogs/pb/cspb/pb_gateway";
        private const string PkgChat = "github.com/gogu-x/gogs/pb/cspb/pb_chat";
        private const string PkgActivity = "github.com/gogu-x/gogs/pb/cspb/pb_activity";
        private const string PkgGuild = "github.com/gogu-x/gogs/pb/cspb/pb_guild";
        private const string PkgBattle = "github.com/gogu-x/gogs/pb/cspb/pb_battle";
        private const string PkgEquipment = "github.com/gogu-x/gogs/pb/cspb/pb_equipment";

        // msgTypeName -> 完整 Go 包路径。新增 proto message 时需要在此登记，
        // 否则 Encode/Decode 时找不到对应的 msgID 映射。
        private static readonly Dictionary<string, string> MsgTypeToPkg = new()
        {
            // gateway
            { "LoginReq", PkgGateway },
            { "RegisterReq", PkgGateway },
            { "LogoutReq", PkgGateway },
            { "LoginAck", PkgGateway },
            { "RegisterAck", PkgGateway },
            { "GetServerListReq", PkgGateway },
            { "ServerListAck", PkgGateway },

            // chat
            { "ChatReq", PkgChat },
            { "ChatAck", PkgChat },

            // activity
            { "GetActivityListReq", PkgActivity },
            { "GetActivityListAck", PkgActivity },
            { "JoinActivityReq", PkgActivity },
            { "JoinActivityAck", PkgActivity },
            { "GetProgressReq", PkgActivity },
            { "GetProgressAck", PkgActivity },
            { "ClaimRewardReq", PkgActivity },
            { "ClaimRewardAck", PkgActivity },

            // guild
            { "CreateGuildReq", PkgGuild },
            { "CreateGuildAck", PkgGuild },
            { "JoinGuildReq", PkgGuild },
            { "JoinGuildAck", PkgGuild },
            { "LeaveGuildReq", PkgGuild },
            { "LeaveGuildAck", PkgGuild },
            { "GetGuildReq", PkgGuild },
            { "GetGuildAck", PkgGuild },

            // battle
            { "StartBattleReq", PkgBattle },
            { "StartBattleAck", PkgBattle },
            { "BattleCreatedNtf", PkgBattle },
            { "BattleActionNtf", PkgBattle },
            { "BattleFinishedNtf", PkgBattle },

            // equipment
            { "GetRoleEquipmentReq", PkgEquipment },
            { "GetRoleEquipmentAck", PkgEquipment },
            { "ChangeRoleEquipmentReq", PkgEquipment },
            { "ChangeRoleEquipmentAck", PkgEquipment },
        };

        private static readonly Dictionary<ushort, string> IdToMsgType = new();
        private static readonly Dictionary<string, ushort> MsgTypeToId = new();

        static MsgIdRegistry()
        {
            foreach (var kv in MsgTypeToPkg)
            {
                var msgType = kv.Key;
                var pkg = kv.Value;
                var id = Fnv32a.HashUint16(pkg + "." + msgType);
                MsgTypeToId[msgType] = id;
                IdToMsgType[id] = msgType;
            }
        }

        public static bool TryGetId(string msgType, out ushort id) => MsgTypeToId.TryGetValue(msgType, out id);

        public static bool TryGetMsgType(ushort id, out string msgType) => IdToMsgType.TryGetValue(id, out msgType);
    }
}
