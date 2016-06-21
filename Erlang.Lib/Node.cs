using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Erlang.Lib
{
    public sealed class Node
    {
        private readonly string _name;
        private readonly ushort _port;
        private readonly ushort _distVersion;
        private readonly EpmdConnection _epmdConn;
        private readonly TcpListener _tcpListener;
        private bool _listening;

        internal string Key => $"{_name}:{_port}";

        internal Node(string name, ushort port, ushort distVersion, EpmdConnection epmdConn)
        {
            _name = name;
            _port = port;
            _distVersion = distVersion;
            _epmdConn = epmdConn;

            _tcpListener = TcpListener.Create(port);
            _tcpListener.Start();
            _listening = true;
            Task.Factory.StartNew(AcceptConnections);
        }

        public async Task<bool> Connect(string name)
        {
            var port = await _epmdConn.GetDistributionPort(name);
            if (port <= 0)
            {
                return false;
            }

            var client = new TcpClient();
            await client.ConnectAsync(Dns.GetHostName(), port);

            return true;
        }

        public void Shutdown()
        {
            _listening = false;
            _tcpListener.Stop();
            _epmdConn.UnregisterNode(this);
        }

        private async Task AcceptConnections()
        {
            while (_listening)
            {
                var client = await _tcpListener.AcceptTcpClientAsync();
                var stream = client.GetStream();

                var receiveName = await DistributionHandshake.ReceiveName(stream);
                ;
            }
        }
    }
}
