using System;
using UnityEngine;

namespace XiuXian.Network
{
    /// <summary>Localizes the shared protocol enum. Server responses never supply UI error text.</summary>
    public static class LoadoutErrors
    {
        public const string UnknownCodeMessage = "操作未完成，请刷新后重试";

        public static bool IsKnown(int code)
        {
            switch (code)
            {
                case ErrCode.Ok:
                case ErrCode.Unknown:
                case ErrCode.Param:
                case ErrCode.Unauthorized:
                case ErrCode.LoginInProgress:
                case ErrCode.ServerNotFound:
                case ErrCode.UsernameExists:
                case ErrCode.WrongPassword:
                case ErrCode.Internal:
                case ErrCode.AccountNotFound:
                case ErrCode.GuildExists:
                case ErrCode.GuildNotFound:
                case ErrCode.AlreadyInGuild:
                case ErrCode.NotInGuild:
                case ErrCode.LoadoutUnavailable:
                case ErrCode.ConfigUnavailable:
                case ErrCode.StaleRevision:
                case ErrCode.InvalidSlot:
                case ErrCode.ItemNotOwned:
                case ErrCode.InvalidItemState:
                case ErrCode.EquipmentUnavailable:
                case ErrCode.InvalidEquipment:
                case ErrCode.DuplicateJade:
                case ErrCode.MaxLevelReached:
                case ErrCode.InsufficientEssence:
                case ErrCode.AffixGenerationFailed:
                case ErrCode.InvalidTalentNode:
                case ErrCode.TalentAlreadyLearned:
                case ErrCode.TalentPrerequisiteMissing:
                case ErrCode.InsufficientTalentPoints:
                case ErrCode.InvalidTalentBranch:
                case ErrCode.BattleUnavailable:
                case ErrCode.BattleTimeout:
                case ErrCode.BattleCreateFailed:
                case ErrCode.InvalidBattleRequest:
                case ErrCode.BattleInProgress:
                case ErrCode.ActivityUnavailable:
                case ErrCode.ActivityNotFound:
                case ErrCode.ActivityNotOpen:
                case ErrCode.ActivityNotJoined:
                case ErrCode.ActivityAlreadyJoined:
                case ErrCode.ActivityRewardNotReady:
                case ErrCode.ActivityRewardAlreadyClaimed:
                    return true;
                default: return false;
            }
        }

        public static string ToUserMessage(int code)
        {
            switch (code)
            {
                case ErrCode.Ok: return "操作成功";
                case ErrCode.Unknown: return "操作未完成，请稍后重试";
                case ErrCode.Param: return "请求参数无效，请重新操作";
                case ErrCode.Unauthorized: return "登录状态已失效，请重新登录";
                case ErrCode.LoginInProgress: return "登录处理中，请稍候";
                case ErrCode.ServerNotFound: return "区服暂不可用，请重新选择";
                case ErrCode.UsernameExists: return "该账号已存在，请直接登录";
                case ErrCode.WrongPassword: return "账号或密码不正确";
                case ErrCode.Internal: return "服务暂不可用，请稍后重试";
                case ErrCode.GuildExists: return "该宗门名称已被使用";
                case ErrCode.GuildNotFound: return "宗门不存在或已解散";
                case ErrCode.AlreadyInGuild: return "已加入宗门";
                case ErrCode.NotInGuild: return "当前尚未加入宗门";
                case ErrCode.LoadoutUnavailable: return "数据暂不可用，请刷新重试";
                case ErrCode.EquipmentUnavailable: return "装备数据暂不可用，请刷新重试";
                case ErrCode.InvalidEquipment: return "该装备不可用，请刷新后重试";
                case ErrCode.ConfigUnavailable: return "配置尚未准备完成，请稍后重试";
                case ErrCode.StaleRevision: return "数据已更新，请重新操作";
                case ErrCode.InvalidSlot: return "该槽位尚不可用";
                case ErrCode.ItemNotOwned: return "未拥有该物品，请刷新后重试";
                case ErrCode.DuplicateJade: return "同一魂玉不能重复镶嵌，请先从其他槽位卸下";
                case ErrCode.InvalidItemState: return "物品数据异常，请刷新后重试";
                case ErrCode.MaxLevelReached: return "已达到最高等级";
                case ErrCode.InsufficientEssence: return "玉髓不足";
                case ErrCode.AffixGenerationFailed: return "词条生成失败，请稍后重试";
                case ErrCode.InvalidTalentNode: return "该天赋节点尚不可用";
                case ErrCode.TalentAlreadyLearned: return "该天赋已经点亮";
                case ErrCode.TalentPrerequisiteMissing: return "请先点亮前置天赋";
                case ErrCode.InsufficientTalentPoints: return "可用天赋点不足";
                case ErrCode.InvalidTalentBranch: return "该天赋流派尚不可用";
                case ErrCode.BattleUnavailable: return "战斗服务暂不可用，请稍后重试";
                case ErrCode.BattleTimeout: return "战斗服务暂时无响应，请稍后重试";
                case ErrCode.BattleCreateFailed: return "挑战创建失败，请重试";
                case ErrCode.BattleInProgress: return "已有战斗正在进行，请先返回战斗";
                case ErrCode.InvalidBattleRequest: return "战斗请求无效，请重新发起挑战";
                case ErrCode.AccountNotFound: return "账号尚未创建，请先注册";
                case ErrCode.ActivityUnavailable: return "活动服务暂不可用，请稍后重试";
                case ErrCode.ActivityNotFound: return "该活动不存在";
                case ErrCode.ActivityNotOpen: return "该活动尚未开放或已结束";
                case ErrCode.ActivityNotJoined: return "请先参与该活动";
                case ErrCode.ActivityAlreadyJoined: return "已参与该活动";
                case ErrCode.ActivityRewardNotReady: return "尚未满足奖励领取条件";
                case ErrCode.ActivityRewardAlreadyClaimed: return "该活动奖励已领取";
            }
            Debug.LogWarning("[ServerErrors] Unknown ErrCode value: " + (int)code);
            return UnknownCodeMessage;
        }
    }
}
