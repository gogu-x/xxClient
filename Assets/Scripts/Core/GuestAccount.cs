using UnityEngine;

namespace XiuXian.Core
{
    /// <summary>
    /// 临时的游客账号方案：在正式账号系统（注册/登录 UI）接入前，
    /// 用设备唯一标识派生一个固定账号密码，实现"一键登录"，
    /// 不需要修改后端协议（仍然调用现有的 LoginReq/RegisterReq）。
    ///
    /// 局限：换设备或清除本地数据会变成新账号，无法跨设备找回。
    /// 后续接入正式账号系统时，可以在此基础上增加"账号绑定"流程，
    /// 不影响已有游客数据。
    /// </summary>
    public static class GuestAccount
    {
        private const string PrefsKey = "XiuXian.GuestAccountId";

        /// <summary>获取（或首次生成并持久化）本机的游客账号标识。</summary>
        public static string GetOrCreateAccount()
        {
            var saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(saved))
            {
                return saved;
            }

            var deviceId = SystemInfo.deviceUniqueIdentifier;
            // deviceUniqueIdentifier 在部分平台（如 WebGL）不保证稳定，
            // 附加一个本地生成的 GUID 作为兜底，首次生成后持久化到 PlayerPrefs。
            var account = $"guest_{deviceId}_{System.Guid.NewGuid():N}".Substring(0, 32);
            PlayerPrefs.SetString(PrefsKey, account);
            PlayerPrefs.Save();
            return account;
        }

        /// <summary>游客账号统一使用的固定密码（账号本身已经是随机生成的，密码不需要额外随机）。</summary>
        public static string GetPassword() => "guest-default-pwd";
    }
}
