using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace LiveCollab.Net
{
    /// <summary>Background TCP listener. Accepts peers and hands them to a callback.</summary>
    public class TcpListenerCore : IDisposable
    {
        readonly Socket listener;
        readonly Thread acceptThread;
        readonly List<PeerSocket> peers = new List<PeerSocket>();
        volatile bool alive = true;

        public event Action<PeerSocket> OnPeer;

        public int Port { get; private set; }

        public TcpListenerCore(int port)
        {
            Port = port;
            listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listener.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Bind(new IPEndPoint(IPAddress.Any, port));
            listener.Listen(16);
            acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            acceptThread.Start();
        }

        void AcceptLoop()
        {
            while (alive)
            {
                try
                {
                    var s = listener.Accept();
                    var peer = new PeerSocket(s);
                    lock (peers) peers.Add(peer);
                    var cb = OnPeer;
                    if (cb != null) cb(peer);
                }
                catch (Exception)
                {
                    if (!alive) break;
                    Thread.Sleep(20);
                }
            }
        }

        public List<PeerSocket> SnapshotPeers()
        {
            lock (peers)
            {
                var copy = new List<PeerSocket>(peers);
                for (int i = copy.Count - 1; i >= 0; i--)
                {
                    if (!copy[i].Alive)
                    {
                        peers.Remove(copy[i]);
                        copy.RemoveAt(i);
                    }
                }
                return copy;
            }
        }

        public void Dispose()
        {
            alive = false;
            try { listener.Close(); } catch (Exception) { }
            try { acceptThread.Join(200); } catch (Exception) { }
            List<PeerSocket> copy;
            lock (peers)
            {
                copy = new List<PeerSocket>(peers);
                peers.Clear();
            }
            foreach (var p in copy) p.Dispose();
        }
    }
}
