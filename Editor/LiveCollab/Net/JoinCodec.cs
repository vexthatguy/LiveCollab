using System;
using System.Net;
using System.Text;

namespace LiveCollab.Net
{
    /// <summary>Join-code helpers and IP:port parsing.</summary>
    public static class JoinCodec
    {
        // no ambiguous O/0/I/1
        const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        public static string Generate(Random rng)
        {
            if (rng == null) rng = new Random();
            var sb = new StringBuilder(4);
            for (int i = 0; i < 4; i++) sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>Parse "ip" or "ip:port". Port defaults to defaultPort.</summary>
        public static bool TryParseEndpoint(string input, int defaultPort, out IPAddress ip, out int port)
        {
            ip = null;
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(input)) return false;
            input = input.Trim();
            string host = input;
            int colon = input.LastIndexOf(':');
            if (colon > 0)
            {
                string portPart = input.Substring(colon + 1);
                int p;
                if (int.TryParse(portPart, out p) && p > 0 && p < 65536)
                {
                    port = p;
                    host = input.Substring(0, colon);
                }
            }
            return IPAddress.TryParse(host, out ip);
        }
    }
}
