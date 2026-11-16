using System;
using System.Net.Sockets;
using System.Threading;

namespace LiveCollab.Net
{
    /// <summary>Outgoing connection (client side). Connects on a background thread.</summary>
    public class ClientLink : IDisposable
    {
        public PeerSocket Peer;
        Thread connectThread;

        public bool Connected { get { return Peer != null && Peer.Alive; } }

        public void Connect(string ip, int port, Action onSuccess, Action<string> onError)
        {
            connectThread = new Thread(() =>
            {
                var s = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    var asyncResult = s.BeginConnect(ip, port, null, null);
                    if (!asyncResult.AsyncWaitHandle.WaitOne(6000))
                    {
                        try { s.Close(); } catch (Exception) { }
                        if (onError != null) onError("Connection timed out.");
                        return;
                    }
                    s.EndConnect(asyncResult);
                    try { s.NoDelay = true; } catch (Exception) { }
                    Peer = new PeerSocket(s);
                    if (onSuccess != null) onSuccess();
                }
                catch (Exception e)
                {
                    try { s.Close(); } catch (Exception) { }
                    if (onError != null) onError(e.Message);
                }
            }) { IsBackground = true };
            connectThread.Start();
        }

        public void Dispose()
        {
            if (Peer != null)
            {
                Peer.Dispose();
                Peer = null;
            }
        }
    }
}
