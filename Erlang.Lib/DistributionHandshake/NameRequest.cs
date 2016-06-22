using System;
using System.Text;

namespace Erlang.Lib.DistributionHandshake
{
    internal class NameRequest
    {
        private const int MinLength = 10; // {'n', v0a, v0b, v1a, v2b, flag0, flag1, flag2, flag3, name0}
        private const byte Tag = 110; // 'n'

        public ushort HighestVersion { get; }
        public ushort LowestVersion { get; }
        public CapabilityFlags CapabilityFlags { get; }
        public string Name { get; }

        public NameRequest(ushort highestVersion, ushort lowestVersion, CapabilityFlags capabilityFlags, string name)
        {
            HighestVersion = highestVersion;
            LowestVersion = lowestVersion;
            CapabilityFlags = capabilityFlags;
            Name = name;
        }

        public static bool TryParse(byte[] buf, out NameRequest val)
        {
            if (buf.Length < MinLength || buf[0] != Tag)
            {
                val = null;
                return false;
            }

            var version0Buf = new byte[sizeof(ushort)];
            var version1Buf = new byte[sizeof(ushort)];
            var flagsBuf = new byte[sizeof(ushort)];
            var nameLength = buf.Length - version0Buf.Length - version1Buf.Length - flagsBuf.Length - 1;
            var nameBuf = new byte[nameLength];

            var offset = 1;
            Buffer.BlockCopy(buf, offset, version0Buf, 0, version0Buf.Length);
            offset += version0Buf.Length;
            Buffer.BlockCopy(buf, offset, version1Buf, 0, version1Buf.Length);
            offset += version1Buf.Length;
            Buffer.BlockCopy(buf, offset, flagsBuf, 0, flagsBuf.Length);
            offset += flagsBuf.Length;
            Buffer.BlockCopy(buf, offset, nameBuf, 0, nameBuf.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(version0Buf);
                Array.Reverse(version1Buf);
                Array.Reverse(flagsBuf);
            }
            
            var highestVersion = BitConverter.ToUInt16(version0Buf, 0);
            var lowestVersion = BitConverter.ToUInt16(version1Buf, 0);
            var capabilityFlags = BitConverter.ToUInt16(flagsBuf, 0);
            var name = Encoding.UTF8.GetString(nameBuf);
            val = new NameRequest(highestVersion, lowestVersion, (CapabilityFlags)capabilityFlags, name);
            return true;
        }
    }
}
