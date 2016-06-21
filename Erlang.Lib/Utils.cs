using System;
using System.Net;
using System.Net.Sockets;

namespace Erlang.Lib
{
    public static class Utils
    {
        /// <summary>
        /// Gets a free tcp port.
        /// http://stackoverflow.com/a/150974
        /// </summary>
        /// <returns>The tcp port number.</returns>
        public static int FreeTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint) listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        /// <summary>
        /// Adds a 16 bit integer to the front of buf that corresponds to the length of buf.
        /// </summary>
        /// <param name="buf">The byte buffer to edit.</param>
        /// <returns>The byte buffer with a message length prefix.</returns>
        public static byte[] PrefixBufferLength(byte[] buf)
        {
            var length = (ushort)buf.Length;
            var lengthBuf = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBuf);
            }
            var wrappedBuf = new byte[length + 2];
            Buffer.BlockCopy(lengthBuf, 0, wrappedBuf, 0, lengthBuf.Length);
            Buffer.BlockCopy(buf, 0, wrappedBuf, lengthBuf.Length, length);
            return wrappedBuf;
        }
    }
}
