using Erlang.Lib.DistributionHandshake;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.Lib.Extensions
{
    internal static class Extensions
    {
        /// <summary>
        /// Adds a 16 bit integer to the front of buf that corresponds to the length of buf.
        /// </summary>
        /// <param name="buf">The byte buffer to edit.</param>
        /// <returns>The byte buffer with a message length prefix.</returns>
        internal static byte[] PrefixBufferLength(this byte[] buf)
        {
            var length = (ushort)buf.Length;
            var lengthBuf = BitConverter.GetBytes(length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBuf);
            }
            var wrappedBuf = new byte[length + lengthBuf.Length];
            Buffer.BlockCopy(lengthBuf, 0, wrappedBuf, 0, lengthBuf.Length);
            Buffer.BlockCopy(buf, 0, wrappedBuf, lengthBuf.Length, length);
            return wrappedBuf;
        }

        /// <summary>
        /// Reads a 16 bit unsigned integer from a network stream.
        /// </summary>
        /// <param name="stream">Network stream to read from.</param>
        /// <returns>16 bit unsigned integer read from stream.</returns>
        internal static async Task<ushort> ReadUShortAsync(this NetworkStream stream)
        {
            var buf = new byte[sizeof(ushort)];
            await stream.ReadAsync(buf, 0, buf.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(buf);
            }
            return BitConverter.ToUInt16(buf, 0);
        }

        /// <summary>
        /// Converts a status enum to a byte array.
        /// </summary>
        /// <param name="status">Status of a handshake request.</param>
        /// <returns>Status as a byte array.</returns>
        internal static byte[] ToByteArray(this Status status)
        {
            const byte tag = 115; // 's'
            string text;
            switch(status)
            {
                case Status.Alive:
                    text = "alive";
                    break;
                case Status.Nok:
                    text = "nok";
                    break;
                case Status.NotAllowed:
                    text = "not_allowed";
                    break;
                case Status.Ok:
                    text = "ok";
                    break;
                case Status.OkSimultaneous:
                    text = "ok_simultaneous";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status));
            }
            var tagBuf = new byte[] { tag };
            var textBuf = Encoding.UTF8.GetBytes(text);
            var statusBuf = new byte[tagBuf.Length + textBuf.Length];
            Buffer.BlockCopy(tagBuf, 0, statusBuf, 0, tagBuf.Length);
            Buffer.BlockCopy(textBuf, 0, statusBuf, tagBuf.Length, textBuf.Length);
            return statusBuf;
        }
    }
}
