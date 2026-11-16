using System;
using UnityEngine;
using LiveCollab.Net;

namespace LiveCollab.Core
{
    /// <summary>The joining session (client side).</summary>
    public class ClientSession
    {
        readonly Orchestrator owner;
        ClientLink link;

        public int MyUid = -1;
        public Color MyColor = new Color(0.13f, 0.77f, 0.91f);
        public string Address;
        public int Port;
        public string Role = ""; // set to "owner" when acting as the external-companion host

        public bool Connected { get { return link != null && link.Connected; } }

        float lastDiffTime = -1f;
        float lastPingTime = -1f;
        bool pingPending;

        public ClientSession(Orchestrator o)
        {
            owner = o;
        }

        public void Connect(string address, int port)
        {
            Address = address;
            Port = port;
            link = new ClientLink();
            link.Connect(address, port, OnConnected, OnConnectError);
        }

        void OnConnected()
        {
            var hello = new HelloData { name = owner.DisplayName, ver = "0.2.0", role = Role };
            Send(Msg.Hello, hello);
        }

        void OnConnectError(string msg)
        {
            Debug.LogError("[LiveCollab] Join failed: " + msg);
            owner.StopAll();
        }

        public void Tick(float now)
        {
            if (link == null || !link.Connected) return;
            var peer = link.Peer;

            string json;
            while (peer.TryDequeue(out json))
                HandleMessage(json);

            var profile = owner.Profile;

            if (now - lastDiffTime >= profile.TickIntervalMs * 0.001f)
            {
                lastDiffTime = now;
                var cap = owner.Mirror.Capture();
                var diff = owner.Mirror.Diff(cap);
                if (diff.added.Count > 0 || diff.changed.Count > 0 || diff.removed.Count > 0 || diff.comps.Count > 0)
                    Send(Msg.Diff, diff);
            }

            if (now - lastPingTime >= 1f)
            {
                lastPingTime = now;
                if (!pingPending)
                {
                    pingPending = true;
                    Send(Msg.Ping, new PingData { ts = DateTime.UtcNow.Ticks });
                }
            }
        }

        void HandleMessage(string json)
        {
            var env = Msg.Unpack(json);
            if (env == null) return;
            switch (env.t)
            {
                case Msg.Welcome:
                    var w = JsonUtility.FromJson<WelcomeData>(env.d);
                    if (w != null)
                    {
                        MyUid = w.uid;
                        MyColor = Orchestrator.HexToColor(w.color);
                    }
                    break;
                case Msg.Scene:
                    var scene = JsonUtility.FromJson<SceneData>(env.d);
                    if (scene != null)
                    {
                        int created = owner.Mirror.ApplyScene(scene);
                        Debug.Log("[LiveCollab] Scene snapshot applied (" + created + " new objects).");
                    }
                    break;
                case Msg.Diff:
                    var diff = JsonUtility.FromJson<DiffData>(env.d);
                    if (diff != null) owner.Mirror.ApplyDiff(diff);
                    break;
                case Msg.Pres:
                    var pres = JsonUtility.FromJson<PresenceData>(env.d);
                    if (pres != null) ApplyPresence(pres);
                    break;
                case Msg.Pong:
                    var pong = JsonUtility.FromJson<PingData>(env.d);
                    if (pong != null && pingPending)
                    {
                        pingPending = false;
                        long rtt = (DateTime.UtcNow.Ticks - pong.ts) / 10000L;
                        owner.LastPingMs = rtt;
                    }
                    break;
            }
        }

        void ApplyPresence(PresenceData pres)
        {
            var user = new RemoteUser
            {
                Uid = pres.uid,
                Name = string.IsNullOrEmpty(pres.name) ? "Player " + pres.uid : pres.name,
                Color = Orchestrator.HexToColor(pres.color),
                Position = new Vector3(pres.x, pres.y, pres.z),
                Age = 0f
            };
            owner.Presence.Users[pres.uid] = user;
        }

        public void Send(string type, object data)
        {
            if (link != null && link.Connected) link.Peer.Send(Msg.Pack(type, data));
        }

        public void Stop()
        {
            if (link != null)
            {
                try
                {
                    if (link.Connected) link.Peer.Send(Msg.Pack(Msg.Leave, "{}"));
                }
                catch (Exception) { }
                link.Dispose();
                link = null;
            }
            MyUid = -1;
        }
    }
}
