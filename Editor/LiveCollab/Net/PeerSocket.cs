using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LiveCollab.Net
{
    /// <summary>
    /// Thin framing layer over a socket: length-prefixed UTF-8 JSON frames.
    /// One background reader thread + one background writer thread per peer.
    /// </summary>
    public class PeerSocket : IDisposable
    {
        const int MaxFrameBytes = 16 * 1024 * 1024;

        readonly Socket sock;
        readonly Thread reader;
        readonly Thread writer;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> outbox = new ConcurrentQueue<string>();
        volatile bool alive = true;

        public bool Alive { get { return alive; } }

        public PeerSocket(Socket socket)
        {
            sock = socket;
            try { sock.NoDelay = true; } catch (Exception) { }
            reader = new Thread(ReadLoop) { IsBackground = true };
            writer = new Thread(WriteLoop) { IsBackground = true };
            reader.Start();
            writer.Start();
        }

        void ReadLoop()
        {
            try
            {
                var lenBuf = new byte[4];
                while (alive)
                {
                    int got = 0;
                    while (got < 4)
                    {
                        int n = sock.Receive(lenBuf, got, 4 - got, SocketFlags.None);
                        if (n <= 0) throw new IOException("socket closed");
                        got += n;
                    }
                    int len = BitConverter.ToInt32(lenBuf, 0);
                    if (len < 0 || len > MaxFrameBytes) throw new IOException("bad frame length");
                    var buf = new byte[len];
                    int rd = 0;
                    while (rd < len)
                    {
                        int n = sock.Receive(buf, rd, len - rd, SocketFlags.None);
                        if (n <= 0) throw new IOException("socket closed");
                        rd += n;
                    }
                    inbox.Enqueue(Encoding.UTF8.GetString(buf));
                }
            }
            catch (Exception)
            {
                // peer went away
            }
            finally
            {
                alive = false;
                TryClose();
            }
        }

        void WriteLoop()
        {
            try
            {
                while (alive)
                {
                    string frame;
                    if (!outbox.TryDequeue(out frame))
                    {
                        Thread.Sleep(2);
                        continue;
                    }
                    var bytes = Encoding.UTF8.GetBytes(frame);
                    var header = BitConverter.GetBytes(bytes.Length);
                    SendAll(header);
                    SendAll(bytes);
                }
            }
            catch (Exception)
            {
                alive = false;
                TryClose();
            }
        }

        void SendAll(byte[] data)
        {
            int sent = 0;
            while (sent < data.Length)
            {
                int n = sock.Send(data, sent, data.Length - sent, SocketFlags.None);
                if (n <= 0) throw new IOException("send failed");
                sent += n;
            }
        }

        public void Send(string json)
        {
            if (alive) outbox.Enqueue(json);
        }

        public bool TryDequeue(out string json)
        {
            return inbox.TryDequeue(out json);
        }

        void TryClose()
        {
            try { sock.Close(); } catch (Exception) { }
        }

        public void Dispose()
        {
            alive = false;
            TryClose();
            try { reader.Join(200); } catch (Exception) { }
            try { writer.Join(200); } catch (Exception) { }
        }
    }
}
