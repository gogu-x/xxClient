using System;

namespace XiuXian.Network
{
    /// <summary>
    /// 对齐后端 gogs/codec.ProtoCodec 的 WebSocket 帧编解码。
    /// 格式：| 2字节 msgID (BigEndian) | protobuf 消息体 |
    ///
    /// 注意：这跟 gate↔game 内部使用的 protoGateway.Frame（gRPC/NATS 传输单元）
    /// 是两层不同的东西——客户端到 gate 的 WebSocket 帧只有 msgID+body，
    /// gate 收到后才会在内部包装成 Frame 转发给 game。
    /// </summary>
    public static class FrameCodec
    {
        private const int HeaderSize = 2;

        /// <summary>
        /// 编码一条消息。msgType 为 proto message 名（如 "LoginReq"），
        /// 需要已在 MsgIdRegistry 中登记，否则抛异常。
        /// </summary>
        public static byte[] Encode(string msgType, byte[] payload)
        {
            if (!MsgIdRegistry.TryGetId(msgType, out var id))
            {
                throw new InvalidOperationException($"FrameCodec: msgType '{msgType}' not registered in MsgIdRegistry");
            }

            var buf = new byte[HeaderSize + payload.Length];
            buf[0] = (byte)(id >> 8);
            buf[1] = (byte)(id & 0xFF);
            Buffer.BlockCopy(payload, 0, buf, HeaderSize, payload.Length);
            return buf;
        }

        /// <summary>
        /// 解码收到的原始字节，输出消息类型名和 protobuf body。
        /// 若 msgID 未在 MsgIdRegistry 中登记，或数据过短，返回 false。
        /// </summary>
        public static bool TryDecode(byte[] raw, out string msgType, out byte[] payload)
        {
            msgType = null;
            payload = null;

            if (raw == null || raw.Length < HeaderSize)
            {
                return false;
            }

            ushort id = (ushort)((raw[0] << 8) | raw[1]);
            if (!MsgIdRegistry.TryGetMsgType(id, out msgType))
            {
                return false;
            }

            payload = new byte[raw.Length - HeaderSize];
            Buffer.BlockCopy(raw, HeaderSize, payload, 0, payload.Length);
            return true;
        }
    }
}
