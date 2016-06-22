using Erlang.Lib.DistributionHandshake;
using Erlang.Lib.Extensions;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Erlang.Lib
{
    public sealed class Node
    {
        private const CapabilityFlags DefaultCapabilities =
            CapabilityFlags.ExtendedReferences |
            //CapabilityFlags.DistMonitor |
            //CapabilityFlags.HiddenAtomCache |
            CapabilityFlags.NewFunTags |
            CapabilityFlags.ExtendedPidsPorts |
            CapabilityFlags.ExportPtrTag |
            CapabilityFlags.BitBinaries |
            CapabilityFlags.NewFloats |
            CapabilityFlags.UnicodeIO |
            //CapabilityFlags.DistHDRAtomCache |
            CapabilityFlags.SmallAtomTags |
            CapabilityFlags.UTF8Atoms;

        private readonly string _name;
        private readonly string _ip;
        private readonly ushort _port;
        private readonly ushort _distVersion;
        private readonly string _cookie;
        private readonly EpmdConnection _epmdConn;
        private bool _listening;
        private readonly TcpListener _tcpListener;
        private ConcurrentDictionary<string, CancellationTokenSource> _inProgressConnections;
        private ConcurrentDictionary<string, TcpClient> _activeConnections;

        private string Name => $"{_name}@{_ip}";

        internal string Key => $"{_name}:{_port}";

        internal Node(string name, ushort port, ushort distVersion, EpmdConnection epmdConn)
        {
            _name = name;
            _port = port;
            _distVersion = distVersion;
            _epmdConn = epmdConn;
            _ip = Utils.GetLocalIPAddress();
            _cookie = Utils.GetCookie();
            _inProgressConnections = new ConcurrentDictionary<string, CancellationTokenSource>();
            _activeConnections = new ConcurrentDictionary<string, TcpClient>();
            _tcpListener = TcpListener.Create(port);
            _tcpListener.Start();
            _listening = true;
            Task.Factory.StartNew(AcceptConnections);
        }

        /// <summary>
        /// Attempts to connect to another node.
        /// </summary>
        /// <param name="name">Name of the node.</param>
        /// <param name="ip">Ip of the node.</param>
        /// <returns>Status of the connection.</returns>
        public async Task<bool> TryConnect(string name, IPAddress ip)
        {
            var port = await _epmdConn.GetDistributionPort(name);
            if (port <= 0)
            {
                return false;
            }

            var client = new TcpClient();
            await client.ConnectAsync(ip, port);

            return true;
        }

        /// <summary>
        /// Shuts down a node.
        /// </summary>
        public void Shutdown()
        {
            _listening = false;
            _tcpListener.Stop();
            _epmdConn.UnregisterNode(this);
        }

        /// <summary>
        /// Accepts node connect handshake requests while <see cref="_listening"/> is true.
        /// </summary>
        private async Task AcceptConnections()
        {
            while (_listening)
            {
                var client = await _tcpListener.AcceptTcpClientAsync();
                HandleIncomingDistributionHandshake(client);
            }
        }

        /// <summary>
        /// Fires off a thread to handle an incoming handshake.
        /// </summary>
        /// <param name="client">Tcp connection.</param>
        private void HandleIncomingDistributionHandshake(TcpClient client)
            => Task.Factory.StartNew(() => DoIncomingDistributionHandshake(client));

        /// <summary>
        /// Performs an incoming distribution handshake with another node.
        /// </summary>
        /// <param name="client">Tcp connect to other node.</param>
        private async Task DoIncomingDistributionHandshake(TcpClient client)
        {
            var stream = client.GetStream();
            
            var nameRequest = await ReceiveNameRequest(stream);
            if(nameRequest == null)
            {
                client.Close();
                return;
            }

            var shouldContinue = await SendStatus(nameRequest.Name, stream);
            if(!shouldContinue)
            {
                client.Close();
                return;
            }

            var challenge = await SendChallenge(stream);

            var challengeReply = await ReceiveChallengeReply(stream);
            if(challengeReply == null)
            {
                client.Close();
                return;
            }

            var passed = await SendChallengeAck(challenge, challengeReply.Challenge, challengeReply.Digest, stream);
            if(!passed)
            {
                client.Close();
                return;
            }

            // ensure ack was accepted

            _activeConnections.AddOrUpdate(nameRequest.Name, client, (_key, oldClient) =>
            {
                oldClient.Close();
                return client;
            });
            return;
        }

        /// <summary>
        /// Step 1 for an incoming handshake request.
        /// </summary>
        /// <param name="stream">Stream between nodes.</param>
        /// <returns>Parsed receive name request.</returns>
        private static async Task<NameRequest> ReceiveNameRequest(NetworkStream stream)
        {
            var messageLength = await stream.ReadUShortAsync();

            var nameRequestBuf = new byte[messageLength];
            await stream.ReadAsync(nameRequestBuf, 0, messageLength);
            NameRequest nameRequest;
            return NameRequest.TryParse(nameRequestBuf, out nameRequest) ? nameRequest : null;
        }

        /// <summary>
        /// Step 2 for an incoming handshake request.
        /// </summary>
        /// <param name="name">Full name of the node initiating the handshake.</param>
        /// <param name="stream">Stream between nodes.</param>
        private async Task<bool> SendStatus(string name, NetworkStream stream)
        {
            if (_activeConnections.ContainsKey(name))
            {
                // We are already connected to this node, so send an 'Alive' status.
                var aliveBuf = Status.Alive.ToByteArray()
                    .PrefixBufferLength();
                await stream.WriteAsync(aliveBuf, 0, aliveBuf.Length);

                if (await ReceiveStatusReply(stream))
                {
                    // They want us to terminate the existing connection and continue the handshake.
                    TcpClient existingConnection;
                    if (_activeConnections.TryRemove(name, out existingConnection))
                    {
                        existingConnection.Close();
                    }
                }
                else
                {
                    // Request was a mistake, end the handshake.
                    return false;
                }
            }
            else if (_inProgressConnections.ContainsKey(name))
            {
                // We have an in-progress handshake, need to figure out who should quit.
                var comparison = string.CompareOrdinal(Name, name);
                if (comparison > 0)
                {
                    // Handshake will not continue, so send an 'Nok' status.
                    var nokBuf = Status.Nok.ToByteArray()
                        .PrefixBufferLength();
                    await stream.WriteAsync(nokBuf, 0, nokBuf.Length);
                    return false;
                }

                // Cancel our in-progress request and handshake will continue A-OK
                CancellationTokenSource tokenSource;
                if (_inProgressConnections.TryRemove(name, out tokenSource))
                {
                    tokenSource.Cancel();
                }
                var oksBuf = Status.OkSimultaneous.ToByteArray()
                        .PrefixBufferLength();
                await stream.WriteAsync(oksBuf, 0, oksBuf.Length);
            }
            else
            {
                // Send normal `ok` status.
                var okBuf = Status.Ok.ToByteArray()
                        .PrefixBufferLength();
                await stream.WriteAsync(okBuf, 0, okBuf.Length);
            }
            return true;
        }

        /// <summary>
        /// Step 2b for an incoming handshake request.  
        /// Required if the <see cref="Status.Alive"/> was previously sent as the status.
        /// </summary>
        /// <param name="stream">Stream between nodes.</param>
        /// <returns>Whether the existing connection should be terminated and the current handshake should continue, 
        /// or the current connection attempt was a mistake and should end.</returns>
        private static async Task<bool> ReceiveStatusReply(NetworkStream stream)
        {
            var messageLength = await stream.ReadUShortAsync();
            var replyBuf = new byte[messageLength];
            await stream.ReadAsync(replyBuf, 0, messageLength);
            var reply = Encoding.UTF8.GetString(replyBuf);
            bool result;
            bool.TryParse(reply, out result);
            return result;
        }

        /// <summary>
        /// Step 3 for an incoming handshake request.
        /// </summary>
        /// <param name="stream">Stream between nodes.</param>
        /// <returns>The challenge that was sent.</returns>
        private async Task<string> SendChallenge(NetworkStream stream)
        {
            var challengeRequest = new ChallengeRequest(_distVersion, _distVersion, DefaultCapabilities, Name);

            var challengeRequestBuf = challengeRequest.ToByteArray()
                .PrefixBufferLength();
            await stream.WriteAsync(challengeRequestBuf, 0, challengeRequestBuf.Length);

            uint challengeVal;
            if(BitConverter.IsLittleEndian)
            {
                var challengeCopy = new byte[challengeRequest.Challenge.Length];
                Buffer.BlockCopy(challengeRequest.Challenge, 0, challengeCopy, 0, challengeCopy.Length);
                Array.Reverse(challengeCopy);
                challengeVal = BitConverter.ToUInt32(challengeCopy, 0);
            }
            else
            {
                challengeVal = BitConverter.ToUInt32(challengeRequest.Challenge, 0);
            }
            return challengeVal.ToString();
        }

        /// <summary>
        /// Step 4 for an incoming handshake request.
        /// </summary>
        /// <param name="stream">Stream between nodes.</param>
        /// <returns>A challenge reply.</returns>
        private static async Task<ChallengeReply> ReceiveChallengeReply(NetworkStream stream)
        {
            var messageLength = await stream.ReadUShortAsync();
            var challengeReplyBuf = new byte[messageLength];
            await stream.ReadAsync(challengeReplyBuf, 0, messageLength);
            ChallengeReply challengeReply;
            return ChallengeReply.TryParse(challengeReplyBuf, out challengeReply) ? challengeReply : null;
        }

        /// <summary>
        /// Step 5 for an incoming handshake request.
        /// </summary>
        /// <param name="myChallenge">Challenge sent to the other node.</param>
        /// <param name="yourChallenge">Challenge the other node sent.</param>
        /// <param name="yourDigest">Digest the other node sent.</param>
        /// <param name="stream">Stream between nodes.</param>
        /// <returns>Whether the challenge sent was passed.</returns>
        private async Task<bool> SendChallengeAck(string myChallenge, byte[] yourChallenge, byte[] yourDigest, NetworkStream stream)
        {
            var myCookieBuf = Encoding.UTF8.GetBytes(_cookie);
            var myChallengeBuf = Encoding.UTF8.GetBytes(myChallenge);
            using (var md5 = MD5.Create())
            {
                // Check your digest.
                var digestBuf = new byte[myChallenge.Length + myCookieBuf.Length];
                Buffer.BlockCopy(myCookieBuf, 0, digestBuf, 0, myCookieBuf.Length);
                Buffer.BlockCopy(myChallengeBuf, 0, digestBuf, myCookieBuf.Length, myChallengeBuf.Length);
                var digest = md5.ComputeHash(digestBuf);
                var digestMatch = true;
                for (int i = 0; i < digest.Length && i < yourDigest.Length; i++)
                {
                    if (digest[i] != yourDigest[i])
                    {
                        digestMatch = false;
                    }
                }
                if(!digestMatch)
                {
                    return false;
                }

                // Generate a digest for your challenge.
                var yourDigestBuf = new byte[yourChallenge.Length + myCookieBuf.Length];
                Buffer.BlockCopy(myCookieBuf, 0, yourDigestBuf, 0, myCookieBuf.Length);
                Buffer.BlockCopy(yourChallenge, 0, yourDigestBuf, myCookieBuf.Length, yourChallenge.Length);
                var newDigest = md5.ComputeHash(yourDigestBuf);
                var challengeAck = new ChallengeAck(newDigest);

                var ackBuf = challengeAck.ToByteArray()
                    .PrefixBufferLength();
                await stream.WriteAsync(ackBuf, 0, ackBuf.Length);
                return true;
            }
        }
    }
}
