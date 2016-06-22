using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

namespace Erlang.Lib
{
    public static class Utils
    {
        private static RNGCryptoServiceProvider _rng = new RNGCryptoServiceProvider();

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
        /// Get local IP address.
        /// http://stackoverflow.com/a/7141830
        /// </summary>
        /// <returns>IP address as string.</returns>
        public static string GetLocalIPAddress()
        {
            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
            {
                return null;
            }
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ip = host.AddressList.FirstOrDefault(i => i.AddressFamily == AddressFamily.InterNetwork);
            return ip != null ? ip.ToString() : null;
        }

        /// <summary>
        /// Generates a random int challenge.
        /// </summary>
        /// <returns>Cryptographically secure int challenge.</returns>
        public static byte[] GenChallenge()
        {
            var challenge = new byte[sizeof(uint)];
            _rng.GetBytes(challenge);
            return challenge;
        }

        /// <summary>
        /// Get the cookie string from the user home directory.
        /// </summary>
        /// <returns>The cookie string.</returns>
        public static string GetCookie()
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var cookiePath = Path.Combine(home, ".erlang.cookie");
            if(!File.Exists(cookiePath))
            {
                throw new Exception($"Cookie file not found at '{cookiePath}'");
            }
            return File.ReadAllText(cookiePath);
        }
    }
}
