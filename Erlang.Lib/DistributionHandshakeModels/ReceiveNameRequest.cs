using System;
using System.Text;

namespace Erlang.Lib.DistributionHandshakeModels
{
    public class ReceiveNameRequest
    {
        private const int MinLength = 10; // {'n', v0a, v0b, v1a, v2b, flag0, flag1, flag2, flag3, name0}
        private const byte Tag = 110; // 'n'

        public ushort HighestVersion { get; }
        public ushort LowestVersion { get; }
        public uint CapabilityFlags { get; }
        public string Name { get; }

        internal ReceiveNameRequest(ushort highestVersion, ushort lowestVersion, uint capabilityFlags, string name)
        {
            HighestVersion = highestVersion;
            LowestVersion = lowestVersion;
            CapabilityFlags = capabilityFlags;
            Name = name;
        }

        public static bool TryParse(byte[] buf, out ReceiveNameRequest val)
        {
            if (buf.Length < MinLength || buf[0] != Tag)
            {
                val = null;
                return false;
            }

            var version0Buf = new byte[2];
            var version1Buf = new byte[2];
            var flagsBuf = new byte[4];
            var nameLength = buf.Length - version0Buf.Length - version1Buf.Length - flagsBuf.Length - 1;
            var nameBuf = new byte[nameLength];

            Buffer.BlockCopy(buf, 1, version0Buf, 0, 2);
            Buffer.BlockCopy(buf, 3, version1Buf, 0, 2);
            Buffer.BlockCopy(buf, 5, flagsBuf, 0, 4);
            Buffer.BlockCopy(buf, 9, nameBuf, 0, nameLength);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(version0Buf);
                Array.Reverse(version1Buf);
                Array.Reverse(flagsBuf);
            }
            
            var highestVersion = BitConverter.ToUInt16(version0Buf, 0);
            var lowestVersion = BitConverter.ToUInt16(version1Buf, 0);
            var capabilityFlags = BitConverter.ToUInt32(flagsBuf, 0);
            var name = Encoding.UTF8.GetString(nameBuf);
            val = new ReceiveNameRequest(highestVersion, lowestVersion, capabilityFlags, name);
            return true;
        }
    }
}
