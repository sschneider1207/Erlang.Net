using System;

namespace Erlang.Lib.DistributionHandshake
{
    internal class ChallengeReply
    {
        private const int Length = 21; // {'r', Chal0..3, Dige0..15}
        private const byte Tag = 114; // 'r'

        public byte[] Challenge { get; }
        public byte[] Digest { get; }

        public ChallengeReply(byte[] challenge, byte[] digest)
        {
            Challenge = challenge;
            Digest = digest;
        }
        
        public static bool TryParse(byte[] buf, out ChallengeReply val)
        {
            if(buf.Length != Length || buf[0] != Tag)
            {
                val = null;
                return false;
            }

            var challenge = new byte[sizeof(uint)];
            var digest = new byte[16];

            var offset = 1;
            Buffer.BlockCopy(buf, offset, challenge, 0, challenge.Length);
            offset += challenge.Length;
            Buffer.BlockCopy(buf, offset, digest, 0, digest.Length);
            val = new ChallengeReply(challenge, digest);
            return true;
        }
    }
}
