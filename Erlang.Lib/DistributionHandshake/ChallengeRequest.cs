using System;
using System.Text;

namespace Erlang.Lib.DistributionHandshake
{
    internal class ChallengeRequest
    {
        private const byte Tag = 110; // 'n'

        public ushort HighestVersion { get; }
        public ushort LowestVersion { get; }
        public CapabilityFlags CapabilityFlags { get; }
        public byte[] Challenge { get; }
        public string Name { get; }

        public ChallengeRequest(ushort highestVersion, ushort lowestVersion, CapabilityFlags capabilityFlags, string name)
        {
            HighestVersion = highestVersion;
            LowestVersion = lowestVersion;
            CapabilityFlags = capabilityFlags;
            Challenge = Utils.GenChallenge();
            Name = name;
        }

        /// <summary>
        /// Converts a send challenge request to a byte array.
        /// </summary>
        /// <returns>The converted byte array.</returns>
        public byte[] ToByteArray()
        {
            var tagBuf = new byte[] { Tag };
            var highestVersionBuf = BitConverter.GetBytes(HighestVersion);
            var lowestVersionnBuf = BitConverter.GetBytes(LowestVersion);
            var capabilityFlagsBuf = BitConverter.GetBytes((ushort)CapabilityFlags);
            var nameBuf = Encoding.UTF8.GetBytes(Name);

            var messageBufLength = tagBuf.Length + highestVersionBuf.Length + lowestVersionnBuf.Length +
                capabilityFlagsBuf.Length + Challenge.Length + nameBuf.Length;
            var messageBuf = new byte[messageBufLength];

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(highestVersionBuf);
                Array.Reverse(lowestVersionnBuf);
                Array.Reverse(capabilityFlagsBuf);
            }

            var offset = 0;
            Buffer.BlockCopy(tagBuf, 0, messageBuf, offset, tagBuf.Length);
            offset += tagBuf.Length;
            Buffer.BlockCopy(highestVersionBuf, 0, messageBuf, offset, highestVersionBuf.Length);
            offset += highestVersionBuf.Length;
            Buffer.BlockCopy(lowestVersionnBuf, 0, messageBuf, offset, lowestVersionnBuf.Length);
            offset += lowestVersionnBuf.Length;
            Buffer.BlockCopy(capabilityFlagsBuf, 0, messageBuf, offset, capabilityFlagsBuf.Length);
            offset += capabilityFlagsBuf.Length;
            Buffer.BlockCopy(Challenge, 0, messageBuf, offset, Challenge.Length);
            offset += Challenge.Length;
            Buffer.BlockCopy(nameBuf, 0, messageBuf, offset, nameBuf.Length);

            return messageBuf;
        }
    }
}
