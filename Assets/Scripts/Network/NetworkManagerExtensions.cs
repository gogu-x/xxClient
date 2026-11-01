using System;
using System.Text.RegularExpressions;
using Google.Protobuf;
using UnityEngine;

namespace XiuXian.Network
{
    /// <summary>
    /// NetworkManager 的类型安全扩展方法，业务代码优先使用这层 API 发送/解析消息，
    /// 而不是直接操作 (string msgType, byte[] payload)。
    ///
    /// 消息类型名（msgType）取自 protoc --csharp_out 生成类的类型名
    /// （如 LoginReq、ChatAck），与后端 codec.ProtoCodec 的 msgID 算法
    /// （FNV32a(GoPkgPath + "." + MessageName)）保持一致，
    /// 具体映射见 MsgIdRegistry。
    /// </summary>
    public static class NetworkManagerExtensions
    {
        private const string NetworkLogColor = "#00FF00";

        private static readonly Regex SensitiveJsonField = new(
            "(\"(?:password|token|accessToken|refreshToken|secret|credential|authorization)\"\\s*:\\s*)\"(?:\\\\.|[^\"])*\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>发送一个 Google.Protobuf 生成的消息对象。</summary>
        public static void SendProto(this NetworkManager net, IMessage msg)
        {
            if (msg == null)
            {
                throw new ArgumentNullException(nameof(msg));
            }

            var msgType = msg.Descriptor.Name;
            LogProto("=>", msgType, msg);
            net.Send(msgType, msg.ToByteArray());
        }

        /// <summary>
        /// 注册某个 proto 消息类型的处理回调，自动完成反序列化。
        /// msgType 为消息类名（如 "LoginAck"），parser 为该类型的静态 Parser（如 LoginAck.Parser）。
        /// </summary>
        public static void RegisterProtoHandler<T>(this NetworkManager net, string msgType, MessageParser<T> parser, Action<T> handler)
            where T : IMessage<T>
        {
            net.RegisterHandler(msgType, payload =>
            {
                try
                {
                    var message = parser.ParseFrom(payload);
                    LogProto("<=", msgType, message);
                    handler(message);
                }
                catch (Exception exception)
                {
                    if (msgType.EndsWith("Ack", StringComparison.Ordinal))
                    {
                        string rawPayload = payload == null ? string.Empty : Convert.ToBase64String(payload);
                        Debug.LogWarning(ColorizeNetworkLog($"[Proto] <= {msgType} {{ \"decode_error\": true, \"exception\": \"{exception.GetType().Name}\", \"payload_base64\": \"{rawPayload}\" }}"));
                    }
                    throw;
                }
            });
        }

        private static void LogProto(string direction, string msgType, IMessage message)
        {
            string json;
            try
            {
                json = JsonFormatter.ToDiagnosticString(message);
                json = SensitiveJsonField.Replace(json, "$1\"***\"");
            }
            catch (Exception exception)
            {
                json = $"{{ \"log_error\": \"{exception.GetType().Name}\" }}";
            }

            Debug.Log(ColorizeNetworkLog($"[Proto] {direction} {msgType} {json}"));
        }

        public static string ColorizeNetworkLog(string message)
        {
            return $"<color={NetworkLogColor}>{message}</color>";
        }
    }
}
