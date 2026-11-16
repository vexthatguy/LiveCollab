using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace LiveCollab.Specs
{
    public struct MachineSpecs
    {
        public int CoreCount;
        public float RamGb;
        public int Score;
        public string Summary;
    }

    /// <summary>Reads CPU core count and total RAM without any editor API.</summary>
    public static class HardwareProbe
    {
        public static MachineSpecs Detect()
        {
            var s = new MachineSpecs();
            s.CoreCount = Mathf.Max(1, Environment.ProcessorCount);
            s.RamGb = ReadTotalRamGb();
            if (s.RamGb < 1f) s.RamGb = 4f;

            s.Score = Mathf.RoundToInt(s.CoreCount * 230f + s.RamGb * 90f);
            if (s.CoreCount >= 12) s.Score += 500;
            else if (s.CoreCount >= 8) s.Score += 250;

            s.Summary = s.CoreCount + " cores / " + s.RamGb.ToString("0.#") + " GB RAM (score " + s.Score + ")";
            return s;
        }

        static float ReadTotalRamGb()
        {
            try
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    var status = new MemoryStatusEx();
                    status.dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
                    if (GlobalMemoryStatusEx(ref status))
                        return status.ullTotalPhys / (1024f * 1024f * 1024f);
                }
                else if (Application.platform == RuntimePlatform.OSXEditor)
                {
                    return ReadMacRam();
                }
            }
            catch (Exception) { }
            return 0f;
        }

        static float ReadMacRam()
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo("sysctl", "-n hw.memsize");
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.CreateNoWindow = true;
                using (var p = System.Diagnostics.Process.Start(psi))
                {
                    if (p == null) return 0f;
                    string line = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(2000);
                    double bytes;
                    if (double.TryParse(line, out bytes))
                        return (float)(bytes / (1024.0 * 1024.0 * 1024.0));
                }
            }
            catch (Exception) { }
            return 0f;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
    }
}
