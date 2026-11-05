using System;
using System.Collections.Generic;
using Google.Protobuf;

namespace XiuXian.Network
{
    /// <summary>
    /// gateway.GetServerListReq / ServerListAck 的线协议适配器。
    ///
    /// Unity 当前提交的 protoc 生成文件尚未包含这两个最新消息；这里严格按
    /// gateway_req.proto / gateway_ack.proto 的字段号编码和解析，后续完整重新生成
    /// GatewayReq.cs/GatewayAck.cs 后可直接替换为生成类型。
    /// </summary>
    public static class GatewayServerListProtocol
    {
        public static byte[] EncodeGetServerListReq(string account)
        {
            account ??= string.Empty;
            var payload = new byte[1 + CodedOutputStream.ComputeStringSize(account)];
            var output = new CodedOutputStream(payload);
            output.WriteRawTag(10); // account = 1
            output.WriteString(account);
            output.Flush();
            return payload;
        }

        public static bool TryDecodeServerListAck(byte[] payload, out GatewayServerListResponse response)
        {
            response = new GatewayServerListResponse();
            if (payload == null)
            {
                return false;
            }

            try
            {
                var input = new CodedInputStream(payload);
                uint tag;
                while ((tag = input.ReadTag()) != 0)
                {
                    switch (tag)
                    {
                        case 8: // code = 1
                            response.Code = (global::ErrCode)input.ReadEnum();
                            break;
                        case 26: // accounts = 3
                            response.Accounts.Add(DecodeServerAccount(input.ReadBytes()));
                            break;
                        default:
                            input.SkipLastField();
                            break;
                    }
                }
                return true;
            }
            catch (InvalidProtocolBufferException)
            {
                response = null;
                return false;
            }
        }

        private static GatewayServerAccount DecodeServerAccount(ByteString payload)
        {
            var account = new GatewayServerAccount();
            var input = payload.CreateCodedInput();
            uint tag;
            while ((tag = input.ReadTag()) != 0)
            {
                switch (tag)
                {
                    case 8: // server_id = 1
                        account.ServerId = input.ReadInt32();
                        break;
                    case 16: // uid = 2
                        account.Uid = input.ReadUInt64();
                        break;
                    default:
                        input.SkipLastField();
                        break;
                }
            }
            return account;
        }
    }

    public sealed class GatewayServerListResponse
    {
        public global::ErrCode Code { get; internal set; } = global::ErrCode.Ok;
        public List<GatewayServerAccount> Accounts { get; } = new();
    }

    public sealed class GatewayServerAccount
    {
        public int ServerId { get; internal set; }
        public ulong Uid { get; internal set; }
    }
}
