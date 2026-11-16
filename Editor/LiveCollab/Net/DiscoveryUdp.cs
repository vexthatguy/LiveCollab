using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LiveCollab.Net
{
    /// <summary>
    /// Lightweight LAN discovery. Hosts broadcast a small UDP beacon every 2s;
    /// clients listen and collect hosts. Works on the same local network.
    /// </summary>
    public static class DiscoveryUdp
    {
        public const int DefaultPort = 9595;
        const string Magic = "LIVECOLAB1";

        static UdpClient broadcaster;
        static UdpClient listener;
        static Thread listenThread;
        static volatile bool listening;
        static readonly ConcurrentQueue<Tuple<IPAddress, string>> found =
            new ConcurrentQueue<Tuple<IPAddress, string>>();

        public static void StartBroadcast(string displayName, int tcpPort, string code)
        {
            StopBroadcast();
            try
            {
                broadcaster = new UdpClient();
                broadcaster.EnableBroadcast = true;
                string payload = Magic + "|" + displayName + "|" + LocalIP() + "|" + tcpPort + "|" + code;
                var bytes = Encoding.UTF8.GetBytes(payload);
                var ep = new IPEndPoint(IPAddress.Broadcast, DefaultPort);
                var th = new Thread(() =>
                {
                    while (broadcaster != null)
                    {
                        try { broadcaster.Send(bytes, bytes.Length, ep); }
                        catch (Exception) { }
                        Thread.Sleep(2000);
                    }
                }) { IsBackground = true };
                th.Start();
            }
            catch (Exception) { }
        }

        public static void StopBroadcast()
        {
            try
            {
                if (broadcaster != null) { broadcaster.Close(); broadcaster = null; }
            }
            catch (Exception) { }
        }

        public static void StartListen()
        {
            if (listening) return;
            try { listener = new UdpClient(DefaultPort); }
            catch (Exception) { return; }
            listening = true;
            listenThread = new Thread(ListenLoop) { IsBackground = true };
            listenThread.Start();
        }

        static void ListenLoop()
        {
            var ep = new IPEndPoint(IPAddress.Any, DefaultPort);
            while (listening)
            {
                try
                {
                    var data = listener.Receive(ref ep);
                    var text = Encoding.UTF8.GetString(data);
                    if (text.StartsWith(Magic + "|"))
                        found.Enqueue(Tuple.Create(ep.Address, text.Substring(Magic.Length + 1)));
                }
                catch (Exception) { }
            }
        }

        public static void StopListen()
        {
            listening = false;
            try
            {
                if (listener != null) { listener.Close(); listener = null; }
            }
            catch (Exception) { }
            try { if (listenThread != null) listenThread.Join(200); } catch (Exception) { }
        }

        public static bool TryDequeueFound(out Tuple<IPAddress, string> item)
        {
            return found.TryDequeue(out item);
        }

        public static string LocalIP()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var a in host.AddressList)
                {
                    if (a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a))
                        return a.ToString();
                }
            }
            catch (Exception) { }
            return "127.0.0.1";
        }
    }
}
