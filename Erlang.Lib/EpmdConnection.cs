using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Erlang.Lib
{
    public sealed class EpmdConnection
    {
        private const ushort DefaultEpmdPort = 4369;
        private const ushort DistVersion = 5;

        private readonly ushort _epmdPort;
        private readonly string _hostName;
        private readonly ConcurrentDictionary<string, Tuple<Node, TcpClient>> _registeredNodes; 

        public EpmdConnection() : this(DefaultEpmdPort, Dns.GetHostName()) { }

        public EpmdConnection(ushort epmdPort) : this(epmdPort, Dns.GetHostName()) { }

        public EpmdConnection(string hostname) : this(DefaultEpmdPort, hostname) { }

        public EpmdConnection(ushort epmdPort, string hostName)
        {
            _epmdPort = epmdPort;
            _hostName = hostName;
            _registeredNodes = new ConcurrentDictionary<string, Tuple<Node, TcpClient>>();
        }

        /// <summary>
        /// Registers a new node with the EPMD instance on a random open port.
        /// </summary>
        /// <param name="name">Name of the node.</param>
        /// <returns>The newly registered node.</returns>
        public Task<Node> RegisterNewNode(string name) => RegisterNewNode(name, (ushort)Utils.FreeTcpPort());

        /// <summary>
        /// Registers a new node with the EPMD instance.
        /// </summary>
        /// <param name="name">Name of the node.</param>
        /// <param name="port">Port the node will listen on.</param>
        /// <returns>The newly registered node.</returns>
        public async Task<Node> RegisterNewNode(string name, ushort port)
        {
            var client = new TcpClient();
            await client.ConnectAsync(_hostName, _epmdPort);

            var stream = client.GetStream();

            var reqBuf = Alive2Req(name, port);
            await stream.WriteAsync(reqBuf, 0, reqBuf.Length);

            var respBuf = new byte[4];
            await stream.ReadAsync(respBuf, 0, 4);

            byte result;
            ushort creation;
            if (!ParseAlive2Resp(respBuf, out result, out creation) || result > 0)
            {
                client.Close();
                return null;
            }

            var node = new Node(name, port, DistVersion, this);
            var registration = new Tuple<Node, TcpClient>(node, client);
            _registeredNodes.AddOrUpdate(node.Key, registration, (key, oldRegistration) =>
            {
                var oldClient = oldRegistration.Item2;
                oldClient.Close();
                return registration;
            });
            return node;
        }

        /// <summary>
        /// Unregister a node with the EPMD instance.
        /// </summary>
        /// <param name="node">Node to unregister.</param>
        /// <returns>Status of the unregister request to the EPMD instance.</returns>
        public bool UnregisterNode(Node node)
        {
            Tuple<Node, TcpClient> removedRegistration;
            if (!_registeredNodes.TryRemove(node.Key, out removedRegistration))
            {
                return false;
            }
            var client = removedRegistration.Item2;
            client.Close();
            return true;
        }

        /// <summary>
        /// Get the port number that a node listens to.
        /// </summary>
        /// <param name="nodeName">Name of the node.</param>
        /// <returns>Port number that the node is listening to.</returns>
        public async Task<ushort> GetDistributionPort(string nodeName)
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(_hostName, _epmdPort);
                using (var stream = client.GetStream())
                {
                    var reqBuf = PortPlease2Req(nodeName);
                    await stream.WriteAsync(reqBuf, 0, reqBuf.Length);
                    var respBuf = new byte[4];
                    await stream.ReadAsync(respBuf, 0, 4);
                    ushort port;
                    byte result;
                    ParsePort2Resp(respBuf, out result, out port);
                    return port;
                }
            }
        }

        /// <summary>
        /// Gets an array of string representations of nodes registered by the EPMD.
        /// </summary>
        /// <returns>Array of strings in the form of "node {name} at port {port}".</returns>
        public async Task<string[]> GetAllRegisteredNames()
        {
            using (var client = new TcpClient())
            {
                await client.ConnectAsync(_hostName, _epmdPort);
                using (var stream = client.GetStream())
                {
                    var reqBuf = NamesReq();
                    await stream.WriteAsync(reqBuf, 0, reqBuf.Length);

                    int b;
                    List<byte> bytesList = new List<byte>();
                    while ((b = stream.ReadByte()) != -1)
                    {
                        var val = BitConverter.GetBytes(b);
                        bytesList.Add(val[0]);
                    }

                    var respBuf = bytesList.Skip(4)
                        .ToArray();
                    return Encoding.UTF8.GetString(respBuf)
                        .Split(new[] {'\n'}, StringSplitOptions.RemoveEmptyEntries);
                }
            }
        }

        private static byte[] Alive2Req(string name, ushort port)
        {
            var headerBuf = new byte[] {120};
            
            var portNoBuf = BitConverter.GetBytes(port);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(portNoBuf);
            }

            var nodeTypeBuf = new byte[] {77};

            var protocolBuf = new byte[] {0};

            var distVersionBytes = BitConverter.GetBytes(DistVersion);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(distVersionBytes);
            }

            var nameBuf = Encoding.UTF8.GetBytes(name);
            var nameLength = (ushort) nameBuf.Length;
            var nameLengthBuf = BitConverter.GetBytes(nameLength);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(nameLengthBuf);
            }

            var extraLengthBuf = new byte[] {0, 0};

            var reqLength = (ushort) (headerBuf.Length + portNoBuf.Length + nodeTypeBuf.Length + protocolBuf.Length +
                            distVersionBytes.Length + distVersionBytes.Length + nameLengthBuf.Length + nameBuf.Length + extraLengthBuf.Length);

            var reqBuf = new byte[reqLength];
            var offset = 0;

            Buffer.BlockCopy(headerBuf, 0, reqBuf, offset, headerBuf.Length);
            offset += headerBuf.Length;

            Buffer.BlockCopy(portNoBuf, 0, reqBuf, offset, portNoBuf.Length);
            offset += portNoBuf.Length;

            Buffer.BlockCopy(nodeTypeBuf, 0, reqBuf, offset, nodeTypeBuf.Length);
            offset += nodeTypeBuf.Length;

            Buffer.BlockCopy(protocolBuf, 0, reqBuf, offset, protocolBuf.Length);
            offset += protocolBuf.Length;

            Buffer.BlockCopy(distVersionBytes, 0, reqBuf, offset, distVersionBytes.Length);
            offset += distVersionBytes.Length;

            Buffer.BlockCopy(distVersionBytes, 0, reqBuf, offset, distVersionBytes.Length);
            offset += distVersionBytes.Length;

            Buffer.BlockCopy(nameLengthBuf, 0, reqBuf, offset, nameLengthBuf.Length);
            offset += nameLengthBuf.Length;

            Buffer.BlockCopy(nameBuf, 0, reqBuf, offset, nameBuf.Length);
            offset += nameBuf.Length;

            Buffer.BlockCopy(extraLengthBuf, 0, reqBuf, offset, extraLengthBuf.Length);

            return Utils.PrefixBufferLength(reqBuf);
        }

        private static bool ParseAlive2Resp(byte[] buf, out byte result, out ushort creation)
        {
            result = 0;
            creation = 0;

            if (buf[0] != 121 || buf.Length != 4)
            {
                return false;
            }

            result = buf[1];
            var creationBuf = new byte[2];
            Buffer.BlockCopy(buf, 2, creationBuf, 0, 2);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(creationBuf);
            }
            creation = BitConverter.ToUInt16(creationBuf, 0);
            return true;
        }

        private static byte[] PortPlease2Req(string nodeName)
        {
            var nameBuf = Encoding.UTF8.GetBytes(nodeName);
            var reqLength = 1 + nameBuf.Length;
            var reqBuf = new byte[reqLength];
            reqBuf[0] = 122;
            Buffer.BlockCopy(nameBuf, 0, reqBuf, 1, nameBuf.Length);
            return Utils.PrefixBufferLength(reqBuf);
        }

        private static bool ParsePort2Resp(byte[] buf, out byte result, out ushort port)
        {
            result = 0;
            port = 0;

            if (buf[0] != 119 || buf.Length != 4)
            {
                return false;
            }

            result = buf[1];
            if (result > 0)
            {
                return false;
            }

            var portBuf = new byte[2];
            Buffer.BlockCopy(buf, 2, portBuf, 0, 2);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(portBuf);
            }
            port = BitConverter.ToUInt16(portBuf, 0);
            return true;
        }

        private static byte[] NamesReq() => Utils.PrefixBufferLength(new byte[] {110});
    }
}
