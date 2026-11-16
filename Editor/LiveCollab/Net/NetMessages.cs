using System;
using UnityEngine;

namespace LiveCollab.Net
{
    /// <summary>Wire envelope + handshake payloads.</summary>
    public static class Msg
    {
        public const string Hello = "h";
        public const string Welcome = "w";
        public const string Scene = "s";
        public const string Diff = "d";
        public const string Pres = "p";
        public const string Ping = "pg";
        public const string Pong = "po";
        public const string Leave = "l";

        public static string Pack(string type, object data)
        {
            var e = new Envelope();
            e.t = type;
            if (data is string) e.d = (string)data;       // raw JSON passthrough
            else e.d = JsonUtility.ToJson(data);
            return JsonUtility.ToJson(e);
        }

        public static Envelope Unpack(string json)
        {
            try { return JsonUtility.FromJson<Envelope>(json); }
            catch (Exception) { return null; }
        }
    }

    [Serializable]
    public class Envelope
    {
        public string t;
        public string d;
    }

    [Serializable]
    public class HelloData
    {
        public string name;
        public string ver;
    }

    [Serializable]
    public class WelcomeData
    {
        public int uid;
        public string color = "#22C4E8";
        public int tickMs;
    }

    [Serializable]
    public class PresenceData
    {
        public int uid;
        public string name;
        public string color = "#22C4E8";
        public float x, y, z;
    }

    [Serializable]
    public class PingData
    {
        public long ts;
    }
}
