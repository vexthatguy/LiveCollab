using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LiveCollab.Net;
using LiveCollab.Specs;

namespace LiveCollab.Core
{
    /// <summary>
    /// Top-level coordinator. Owns the current role (host/client/idle),
    /// the scene mirror, presence, and the global editor tick.
    /// </summary>
    public sealed class Orchestrator
    {
        public enum Role { Idle, Host, Client }

        public static Orchestrator Instance { get; private set; }

        static Orchestrator()
        {
            Instance = new Orchestrator();
            EditorApplication.update += Instance.Tick;
            SceneView.duringSceneGui += Instance.OnSceneGUI;
            EditorApplication.quitting += Instance.Shutdown;
        }

        public Role CurrentRole = Role.Idle;
        public HostSession Host;
        public ClientSession Client;
        public readonly PresenceTracker Presence = new PresenceTracker();
        public readonly SceneMirror Mirror = new SceneMirror();
        public SyncProfile Profile = SyncProfile.Resolve(SyncQuality.Auto);
        public SyncQuality QualitySetting = SyncQuality.Auto;
        public string DisplayName = "Creator";
        public long LastPingMs = -1;
        public readonly System.Random Rng = new System.Random();

        float lastTickTime = -1f;
        float lastPresenceTime = -1f;

        Orchestrator() { }

        public bool Active { get { return CurrentRole != Role.Idle; } }

        public void StartHost(int port,        {
            StopAll();
            Mirror.Reset(false);
            Mirror.AllowMatchExisting = false;
            Mirror.UseHighOffsetIds = false;
            Profile = SyncProfile.Resolve(QualitySetting);
            var host = new HostSession(this);
            if (!host.Start(port))
            {
                Debug.LogError("[LiveCollab] Hosting failed — check the port and firewall.");
                return;
            }
            Host = host;
            CurrentRole = Role.Host;
            lastTickTime = -1f;
            Debug.Log("[LiveCollab] Hosting on " + host.AddressText() + " · join code " + host.Code);
        }

        public bool Join(string address, int port)
        {
            StopAll();
            Mirror.Reset(true);
            Mirror.AllowMatchExisting = true;
            Mirror.UseHighOffsetIds = true;
            Profile = SyncProfile.Resolve(QualitySetting);
            var client = new ClientSession(this);
            client.Connect(address, port);
            Client = client;
            CurrentRole = Role.Client;
            lastTickTime = -1f;
            return true;
        }

        public void StopAll()
        {
            if (Host != null) { Host.Stop(); Host = null; }
            if (Client != null) { Client.Stop(); Client = null; }
            Presence.Clear();
            CurrentRole = Role.Idle;
        }

        void Tick()
        {
            try
            {
                if (!Active) return;
                float now = Time.realtimeSinceStartup;
                if (lastTickTime < 0f) lastTickTime = now;

                float dt = now - lastTickTime;
                Presence.UpdateUsers(dt);

                if (CurrentRole == Role.Host) Host.Tick(now);
                else if (CurrentRole == Role.Client) Client.Tick(now);

                if (now - lastPresenceTime >= Profile.PresenceIntervalMs * 0.001f)
                {
                    lastPresenceTime = now;
                    SendPresence();
                }
                lastTickTime = now;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[LiveCollab] Tick error: " + e.Message);
            }
        }

        public Vector3 MyPresencePosition()
        {
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) return sv.pivot;
            if (Selection.activeTransform != null) return Selection.activeTransform.position;
            return Vector3.zero;
        }

        void SendPresence()
        {
            if (CurrentRole == Role.Host && Host == null) return;
            if (CurrentRole == Role.Client && Client == null) return;

            var d = new PresenceData();
            d.uid = CurrentRole == Role.Host ? Host.MyUid : Client.MyUid;
            d.name = DisplayName;
            d.color = ColorToHex(CurrentRole == Role.Host ? Host.MyColor : Client.MyColor);
            Vector3 pos = MyPresencePosition();
            d.x = pos.x; d.y = pos.y; d.z = pos.z;

            if (CurrentRole == Role.Host) Host.Broadcast(Msg.Pres, d, -1);
            else Client.Send(Msg.Pres, d);
        }

        void OnSceneGUI(SceneView sv)
        {
            try { Presence.OnSceneGUI(); } catch (Exception) { }
        }

        public void Shutdown()
        {
            StopAll();
            DiscoveryUdp.StopBroadcast();
            DiscoveryUdp.StopListen();
        }

        public static string ColorToHex(Color c)
        {
            Color32 c32 = c;
            return "#" + c32.r.ToString("X2") + c32.g.ToString("X2") + c32.b.ToString("X2");
        }

        public static Color HexToColor(string hex)
        {
            try
            {
                Color c;
                if (ColorUtility.TryParseHtmlString(hex, out c)) return c;
            }
            catch (Exception) { }
            return new Color(0.13f, 0.77f, 0.91f);
        }
    }
}
