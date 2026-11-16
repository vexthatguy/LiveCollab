using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace LiveCollab.Core
{
    public class RemoteUser
    {
        public int Uid;
        public string Name;
        public Color Color;
        public Vector3 Position;
        public float Age;
    }

    /// <summary>Collaborator presence: floating balls + nametags in the Scene view.</summary>
    public class PresenceTracker
    {
        public readonly Dictionary<int, RemoteUser> Users = new Dictionary<int, RemoteUser>();
        public bool ShowBalls = true;
        public float BallSize = 0.12f;

        readonly List<int> stale = new List<int>();

        public void UpdateUsers(float dt)
        {
            stale.Clear();
            foreach (var kv in Users)
            {
                kv.Value.Age += dt;
                if (kv.Value.Age > 15f) stale.Add(kv.Key);
            }
            foreach (var id in stale) Users.Remove(id);
        }

        public void Remove(int uid)
        {
            if (Users.ContainsKey(uid)) Users.Remove(uid);
        }

        public void Clear()
        {
            Users.Clear();
        }

        public void OnSceneGUI()
        {
            if (!ShowBalls) return;
            foreach (var kv in Users)
            {
                var u = kv.Value;
                if (u == null) continue;
                Handles.color = u.Color;
                Handles.SphereHandleCap(u.Uid, u.Position, Quaternion.identity, BallSize, EventType.Repaint);
                Handles.Label(u.Position + Vector3.up * (BallSize * 0.9f), u.Name, LabelStyle());
            }
        }

        static GUIStyle cachedLabel;
        static GUIStyle LabelStyle()
        {
            if (cachedLabel == null)
            {
                cachedLabel = new GUIStyle(EditorStyles.label);
                cachedLabel.alignment = TextAnchor.MiddleCenter;
                cachedLabel.fontStyle = FontStyle.Bold;
                cachedLabel.normal.textColor = Color.white;
                cachedLabel.fontSize = 12;
            }
            return cachedLabel;
        }
    }
}
