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

        /// <summary>True when hosting through the standalone companion (Python/exe relay).</summary>
        public readonly bool External;
        ClientLink upstream;
        System.Diagnostics.Process companionProc;
        bool companionWarned;

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

        public HostSession(Orchestrator o, bool external = false)
        {
            owner = o;
            External = external;
        }

        public bool CompanionAlive
        {
            get { return companionProc != null && !companionProc.HasExited; }
        }

        public void AttachCompanion(System.Diagnostics.Process proc)
        {
            companionProc = proc;
        }

        public bool Start(int port)
        {
            Port = port;
            Code = JoinCodec.Generate(owner.Rng);

            if (External)
            {
                upstream = new ClientLink();
                upstream.Connect("127.0.0.1", port, () =>
                {
                    var hello = new HelloData { name = owner.DisplayName, ver = "0.2.0", role = "owner" };
                    SendToUpstream(Msg.Hello, hello);
                }, msg => Debug.LogWarning("[LiveCollab] Companion connect failed: " + msg));
                return true;
            }

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
            if (External) { TickExternal(now); return; }
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

        void TickExternal(float now)
        {
            if (upstream == null || !upstream.Connected) return;
            var peer = upstream.Peer;

            string json;
            while (peer.TryDequeue(out json))
                HandleMessage(peer, json);

            if (!companionWarned && companionProc != null && companionProc.HasExited)
            {
                companionWarned = true;
                Debug.LogWarning("[LiveCollab] Companion process exited unexpectedly. Guests can no longer connect.");
            }

            var profile = owner.Profile;
            if (now - lastDiffTime >= profile.TickIntervalMs * 0.001f)
            {
                lastDiffTime = now;
                var cap = owner.Mirror.Capture();
                var diff = owner.Mirror.Diff(cap);
                if (diff.added.Count > 0 || diff.changed.Count > 0 || diff.removed.Count > 0 || diff.comps.Count > 0)
                    SendToUpstream(Msg.Diff, diff);
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
            var pres = JsonUtility.FromJson<PresenceData>(data);
            if (pres == null) return;

            if (External)
            {
                // companion already stamped uid/color and fanned this out to everyone
                ApplyRemotePresence(pres);
                return;
            }

            int uid;
            if (!uidByPeer.TryGetValue(from, out uid)) return;
            pres.uid = uid;
            string n;
            pres.name = nameByPeer.TryGetValue(from, out n) ? n : "Guest";
            ApplyRemotePresence(pres);
            Broadcast(Msg.Pres, pres, uid);
        }

        void ApplyRemotePresence(PresenceData pres)
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

        public void Broadcast(string type, object data, int exceptUid)
        {
            if (External)
            {
                SendToUpstream(type, data);
                return;
            }
            string json = Msg.Pack(type, data);
            foreach (var kv in uidByPeer)
            {
                if (kv.Value == exceptUid) continue;
                kv.Key.Send(json);
            }
        }

        void SendToUpstream(string type, object data)
        {
            if (upstream != null && upstream.Connected)
                upstream.Peer.Send(Msg.Pack(type, data));
        }

        void BroadcastScene(DiffData diff, bool full)
        {
            if (!full && diff.added.Count == 0 && diff.changed.Count == 0 && diff.removed.Count == 0 && diff.comps.Count == 0)
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
            if (External)
            {
                if (upstream != null)
                {
                    try { if (upstream.Connected) upstream.Peer.Send(Msg.Pack(Msg.Leave, "{}")); } catch (Exception) { }
                    upstream.Dispose();
                    upstream = null;
                }
                if (companionProc != null)
                {
                    try { if (!companionProc.HasExited) companionProc.Kill(); } catch (Exception) { }
                    companionProc = null;
                }
                return;
            }

            if (listener != null) { listener.Dispose(); listener = null; }
            uidByPeer.Clear();
            peerByUid.Clear();
            nameByPeer.Clear();
            DiscoveryUdp.StopBroadcast();
        }
    }
}
