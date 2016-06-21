using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Erlang.Lib.DistributionHandshakeModels;

namespace Erlang.Lib
{
    internal static class DistributionHandshake
    {
        public static async Task<ReceiveNameRequest> ReceiveName(NetworkStream stream)
        {
            // get length of message
            var messageLengthBuf = new byte[2];
            await stream.ReadAsync(messageLengthBuf, 0, 2);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(messageLengthBuf);
            }
            var messageLength = BitConverter.ToUInt16(messageLengthBuf, 0);

            // receive name
            var receiveNameBuf = new byte[messageLength];
            await stream.ReadAsync(receiveNameBuf, 0, messageLength);
            ReceiveNameRequest val;
            return ReceiveNameRequest.TryParse(receiveNameBuf, out val) ? val : null;
        }
    }
}
