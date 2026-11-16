using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LiveCollab.Core;
using LiveCollab.Net;
using LiveCollab.Specs;

namespace LiveCollab.UI
{
    public class LiveCollabWindow : EditorWindow
    {
        [MenuItem("Tools/LiveCollab/Open Panel")]
        public static void OpenFromMenu()
        {
            var win = GetWindow<LiveCollabWindow>(false, "LiveCollab", true);
            win.minSize = new Vector2(340, 380);
        }

        string displayName = "";
        string hostPort = "9501";
        string joinInput = "";
        string lastError = "";
        bool showSettings;
        bool repaintQueued;

        class DiscoveredHost
        {
            public string Name;
            public string Ip;
            public int Port;
            public string Code;
        }

        readonly List<DiscoveredHost> discovered = new List<DiscoveredHost>();
        readonly HashSet<string> seenKeys = new HashSet<string>();

        void OnEnable()
        {
            displayName = EditorPrefs.GetString("LiveCollab.Name", Environment.UserName);
            EditorPrefs.SetString("LiveCollab.Name", displayName);
            DiscoveryUdp.StartListen();
            titleContent.image = IconFactory.Icon;
        }

        void OnDisable()
        {
            DiscoveryUdp.StopListen();
        }

        void OnDestroy()
        {
            DiscoveryUdp.StopListen();
        }

        void Update()
        {
            Tuple<System.Net.IPAddress, string> item;
            while (DiscoveryUdp.TryDequeueFound(out item))
            {
                var parts = item.Item2.Split('|');
                if (parts.Length >= 4)
                {
                    var dh = new DiscoveredHost();
                    dh.Name = parts[0];
                    dh.Ip = parts.Length >= 3 && !string.IsNullOrEmpty(parts[2]) ? parts[2] : item.Item1.ToString();
                    int p;
                    int.TryParse(parts[3], out p);
                    dh.Port = p > 0 ? p : 9501;
                    dh.Code = parts.Length >= 5 ? parts[4] : "";
                    string key = dh.Ip + ":" + dh.Port;
                    if (seenKeys.Add(key)) discovered.Add(dh);
                }
                repaintQueued = true;
            }
            if (repaintQueued)
            {
                repaintQueued = false;
                Repaint();
            }
        }

        void OnGUI()
        {
            var o = Orchestrator.Instance;

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("LiveCollab", EditorStyles.boldLabel);

            if (string.IsNullOrEmpty(displayName)) displayName = EditorPrefs.GetString("LiveCollab.Name", Environment.UserName);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Your name", GUILayout.Width(70));
            string newName = EditorGUILayout.TextField(displayName);
            if (newName != displayName)
            {
                displayName = newName;
                EditorPrefs.SetString("LiveCollab.Name", displayName);
            }
            EditorGUILayout.EndHorizontal();
            o.DisplayName = string.IsNullOrEmpty(displayName) ? "Guest" : displayName;

            EditorGUILayout.Space(4);
            DrawStatus(o);
            EditorGUILayout.Space(4);

            if (o.CurrentRole == Orchestrator.Role.Host) DrawHostSection(o);
            else if (o.CurrentRole == Orchestrator.Role.Client) DrawClientSection(o);
            else DrawIdleSections(o);

            EditorGUILayout.Space(6);
            if (GUILayout.Button("Stop session")) o.StopAll();

            EditorGUILayout.Space(6);
            DrawSettings(o);

            if (!string.IsNullOrEmpty(lastError))
                EditorGUILayout.HelpBox(lastError, MessageType.Error);
        }

        void DrawStatus(Orchestrator o)
        {
            string role = o.CurrentRole == Orchestrator.Role.Host ? "Hosting" :
                          o.CurrentRole == Orchestrator.Role.Client ? "Joined" : "Idle";
            string users = "users: " + o.Presence.Users.Count;
            string lat = o.LastPingMs >= 0 ? " · ping " + o.LastPingMs + "ms" : "";
            EditorGUILayout.LabelField(role + " · " + users + lat, EditorStyles.miniLabel);
        }

        void DrawIdleSections(Orchestrator o)
        {
            EditorGUILayout.LabelField("Host", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Host the project from this PC. Friends on the same network can discover " +
                "you automatically; over the internet they can join via your IP:port.",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Port", GUILayout.Width(40));
            hostPort = EditorGUILayout.TextField(hostPort);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("Start hosting"))
            {
                int p;
                if (!int.TryParse(hostPort, out p) || p < 1024 || p > 65535)
                    lastError = "Enter a valid port (1024–65535).";
                else
                {
                    lastError = "";
                    o.StartHost(p);
                }
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Join", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Paste a join code (LAN) or an address like 192.168.1.5:9501.",
                MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            joinInput = EditorGUILayout.TextField(joinInput);
            if (GUILayout.Button("Join", GUILayout.Width(60)))
            {
                lastError = "";
                if (!DoJoin(joinInput, o))
                    lastError = "Could not parse address. Use IP:port or a join code.";
            }
            EditorGUILayout.EndHorizontal();

            if (discovered.Count > 0)
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("Found on this network:", EditorStyles.miniLabel);
                for (int i = 0; i < discovered.Count; i++)
                {
                    var d = discovered[i];
                    EditorGUILayout.BeginHorizontal();
                    string label = d.Name + " (" + d.Ip + ":" + d.Port + ")";
                    if (!string.IsNullOrEmpty(d.Code)) label += " · code " + d.Code;
                    EditorGUILayout.LabelField(label);
                    if (GUILayout.Button("Join", GUILayout.Width(50)))
                    {
                        lastError = "";
                        if (!o.Join(d.Ip, d.Port)) lastError = "Join failed.";
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        bool DoJoin(string input, Orchestrator o)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            input = input.Trim();

            System.Net.IPAddress ip;
            int port;
            if (JoinCodec.TryParseEndpoint(input, 9501, out ip, out port))
                return o.Join(ip.ToString(), port);

            foreach (var d in discovered)
            {
                if (!string.IsNullOrEmpty(d.Code) && d.Code.Equals(input, StringComparison.OrdinalIgnoreCase))
                    return o.Join(d.Ip, d.Port);
            }
            return false;
        }

        void DrawHostSection(Orchestrator o)
        {
            if (o.Host == null) return;
            EditorGUILayout.HelpBox(
                "You are hosting. Same network: share the join code. Internet: share the IP:port " +
                "(requires port forwarding on " + o.Host.Port + ").",
                MessageType.Info);
            EditorGUILayout.LabelField("Join code", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(o.Host.Code, new GUIStyle("LargeLabel"));
            EditorGUILayout.LabelField("Address: " + o.Host.AddressText(), EditorStyles.miniLabel);
            EditorGUILayout.Space(2);
            EditorGUILayout.HelpBox(
                "Tip: first run may trigger a Windows Firewall prompt — allow it so friends can connect.",
                MessageType.None);
        }

        void DrawClientSection(Orchestrator o)
        {
            if (o.Client == null) return;
            EditorGUILayout.HelpBox(
                o.Client.Connected
                    ? "Connected to " + o.Client.Address + ":" + o.Client.Port
                    : "Connecting…",
                MessageType.Info);
            EditorGUILayout.LabelField("Waiting for scene snapshot from host…", EditorStyles.miniLabel);
        }

        void DrawSettings(Orchestrator o)
        {
            showSettings = EditorGUILayout.Foldout(showSettings, "Advanced settings");
            if (!showSettings) return;

            EditorGUILayout.LabelField("Sync speed profile", EditorStyles.boldLabel);
            var q = o.QualitySetting;
            var newQ = (SyncQuality)EditorGUILayout.EnumPopup(q);
            if (newQ != q)
            {
                o.QualitySetting = newQ;
                o.Profile = SyncProfile.Resolve(newQ);
                EditorPrefs.SetInt("LiveCollab.Quality", (int)newQ);
            }
            EditorGUILayout.LabelField(SyncProfile.Describe(o.QualitySetting), EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Detected hardware: " + HardwareProbe.Detect().Summary, EditorStyles.miniLabel);
            EditorGUILayout.Space(4);
            o.Presence.ShowBalls = EditorGUILayout.Toggle("Show collaborator balls (scene view)", o.Presence.ShowBalls);
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox("Changes apply from the next session. Auto mode re-detects your hardware.", MessageType.None);
        }
    }
}
