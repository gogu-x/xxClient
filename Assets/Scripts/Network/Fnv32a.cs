using System.Text;

namespace XiuXian.Network
{
    /// <summary>
    /// FNV-32a 哈希算法实现，与 Go 标准库 hash/fnv.New32a() 结果一致。
    /// </summary>
    public static class Fnv32a
    {
        private const uint OffsetBasis = 2166136261;
        private const uint Prime = 16777619;

        public static uint Hash(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            uint hash = OffsetBasis;
            foreach (var b in bytes)
            {
                hash ^= b;
                hash *= Prime;
            }
            return hash;
        }

        /// <summary>对齐 Go 侧 uint16(h.Sum32()) 的截断行为。</summary>
        public static ushort HashUint16(string s) => (ushort)Hash(s);
    }
}
