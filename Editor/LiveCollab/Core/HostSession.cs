using System;
using System.Collections.Generic;
using UnityEngine;
using LiveCollab.Net;

namespace LiveCollab.Core
{
    /// <summary>The authoritative server session (host side).</summary>
    public class HostSession
    {
        readonly Orchestrator owner;
        TcpListenerCore listener;

        readonly Dictionary<PeerSocket, int> uidByPeer = new Dictionary<PeerSocket, int>();
        readonly Dictionary<int, PeerSocket> peerByUid = new Dictionary<int, PeerSocket>();
        readonly Dictionary<PeerSocket, string> nameByPeer = new Dictionary<PeerSocket, string>();
        readonly List<PeerSocket> toRemove = new List<PeerSocket>();

        static readonly Color[] Palette =
        {
            new Color(0.13f, 0.77f, 0.91f), // cyan
            new Color(0.99f, 0.80f, 0.20f), // yellow
            new Color(0.95f, 0.45f, 0.75f), // pink
            new Color(0.55f, 0.85f, 0.35f), // green
            new Color(0.95f, 0.60f, 0.30f), // orange
            new Color(0.65f, 0.55f, 0.95f), // violet
        };

        public int Port;
        public string Code;
        public readonly int MyUid = 0;
        public Color MyColor { get { return Palette[0]; } }
        public bool Running { get { return listener != null; } }

        int nextUid = 1;
        float lastDiffTime = -1f;
        float lastFullTime = -1f;

        public HostSession(Orchestrator o)
        {
            owner = o;
        }

        public bool Start(int port)
        {
            Port = port;
            Code = JoinCodec.Generate(owner.Rng);
            try { listener = new TcpListenerCore(port); }
            catch (Exception e)
            {
                Debug.LogError("[LiveCollab] Could not bind port " + port + ": " + e.Message);
                listener = null;
                return false;
            }
            DiscoveryUdp.StartBroadcast(owner.DisplayName, port, Code);
            return true;
        }

        public string AddressText()
        {
            return DiscoveryUdp.LocalIP() + ":" + Port;
        }

        public void Tick(float now)
        {
            if (listener == null) return;
            var profile = owner.Profile;

            foreach (var peer in listener.SnapshotPeers())
            {
                string json;
                while (peer.TryDequeue(out json))
                    HandleMessage(peer, json);
                if (!peer.Alive) toRemove.Add(peer);
            }

            if (toRemove.Count > 0)
            {
                foreach (var p in toRemove) DropPeer(p);
                toRemove.Clear();
            }

            if (uidByPeer.Count == 0) return;

            if (now - lastDiffTime >= profile.TickIntervalMs * 0.001f)
            {
                lastDiffTime = now;
                var cap = owner.Mirror.Capture();
                var diff = owner.Mirror.Diff(cap);
                bool full = now - lastFullTime >= 10f;
                if (full) lastFullTime = now;
                BroadcastScene(diff, full);
            }
        }

        void HandleMessage(PeerSocket peer, string json)
        {
            var env = Msg.Unpack(json);
            if (env == null) return;
            switch (env.t)
            {
                case Msg.Hello:
                    var hello = JsonUtility.FromJson<HelloData>(env.d);
                    AddPeer(peer, hello != null && !string.IsNullOrEmpty(hello.name) ? hello.name : "Guest");
                    break;
                case Msg.Diff:
                    var diff = JsonUtility.FromJson<DiffData>(env.d);
                    if (diff != null) owner.Mirror.ApplyDiff(diff);
                    break;
                case Msg.Pres:
                    RelayPresence(peer, env.d);
                    break;
                case Msg.Ping:
                    SendTo(peer, Msg.Pong, env.d);
                    break;
                case Msg.Leave:
                    DropPeer(peer);
                    break;
            }
        }

        void AddPeer(PeerSocket peer, string name)
        {
            if (uidByPeer.ContainsKey(peer)) return;
            int uid = nextUid++;
            uidByPeer[peer] = uid;
            peerByUid[uid] = peer;
            nameByPeer[peer] = name;

            var welcome = new WelcomeData
            {
                uid = uid,
                color = Orchestrator.ColorToHex(Palette[uid % Palette.Length]),
                tickMs = owner.Profile.TickIntervalMs
            };
            SendTo(peer, Msg.Welcome, welcome);

            var cap = owner.Mirror.Capture();
            owner.Mirror.Diff(cap);
            var scene = new SceneData { nodes = cap };
            SendTo(peer, Msg.Scene, scene);
        }

        void DropPeer(PeerSocket peer)
        {
            int uid;
            if (uidByPeer.TryGetValue(peer, out uid))
            {
                uidByPeer.Remove(peer);
                peerByUid.Remove(uid);
                nameByPeer.Remove(peer);
                owner.Presence.Remove(uid);
            }
            peer.Dispose();
        }

        void RelayPresence(PeerSocket from, string data)
        {
            int uid;
            if (!uidByPeer.TryGetValue(from, out uid)) return;
            var pres = JsonUtility.FromJson<PresenceData>(data);
            if (pres == null) return;
            pres.uid = uid;
            string n;
            pres.name = nameByPeer.TryGetValue(from, out n) ? n : "Guest";

            var user = new RemoteUser
            {
                Uid = uid,
                Name = pres.name,
                Color = Orchestrator.HexToColor(pres.color),
                Position = new Vector3(pres.x, pres.y, pres.z),
                Age = 0f
            };
            owner.Presence.Users[uid] = user;

            Broadcast(Msg.Pres, pres, uid);
        }

        public void Broadcast(string type, object data, int exceptUid)
        {
            string json = Msg.Pack(type, data);
            foreach (var kv in uidByPeer)
            {
                if (kv.Value == exceptUid) continue;
                kv.Key.Send(json);
            }
        }

        void BroadcastScene(DiffData diff, bool full)
        {
            if (!full && diff.added.Count == 0 && diff.changed.Count == 0 && diff.removed.Count == 0)
                return;
            string json = Msg.Pack(Msg.Diff, diff);
            foreach (var peer in uidByPeer.Keys) peer.Send(json);
        }

        void SendTo(PeerSocket peer, string type, object data)
        {
            peer.Send(Msg.Pack(type, data));
        }

        public void Stop()
        {
            if (listener != null) { listener.Dispose(); listener = null; }
            uidByPeer.Clear();
            peerByUid.Clear();
            nameByPeer.Clear();
            DiscoveryUdp.StopBroadcast();
        }
    }
}
