using System;

namespace Erlang.Lib.DistributionHandshake
{
    internal class ChallengeAck
    {
        private const byte Tag = 97; //'a'

        public byte[] Digest { get; }

        public ChallengeAck(byte[] digest)
        {
            Digest = digest;
        }

        public byte[] ToByteArray()
        {
            var tagBuf = new byte[] { Tag };
            var ackBuf = new byte[tagBuf.Length + Digest.Length];
            Buffer.BlockCopy(tagBuf, 0, ackBuf, 0, tagBuf.Length);
            Buffer.BlockCopy(Digest, 0, ackBuf, tagBuf.Length, Digest.Length);
            return ackBuf;
        }
    }
}
