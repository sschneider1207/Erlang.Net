using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Erlang.Lib;

namespace TestConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            var conn = new EpmdConnection();
            var node1 = conn.RegisterNewNode("testnode1").Result;
            //var node2 = conn.RegisterNewNode("testnode2").Result;
            //var port = conn.GetDistributionPort("foo").Result;
            //var names = conn.GetAllRegisteredNames().Result;
            //var connectionStatus = node1.Connect(port);
            //conn.UnregisterNode(node1);
            //conn.UnregisterNode(node2);
            Console.ReadKey();
        }
    }
}
