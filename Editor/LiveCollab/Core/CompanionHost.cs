using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace LiveCollab.Core
{
    /// <summary>
    /// Locates and spawns the standalone host companion (Python script or
    /// compiled EXE). The companion is NEVER running between sessions — it is
    /// launched only when hosting starts and killed when hosting stops.
    /// </summary>
    public static class CompanionHost
    {
        public const string ProjectCopyDir = "LiveCollab_Host";
        const string ScriptName = "livecollab_host.py";
        const string ExeName = "livecollab_host.exe";

        static string cachedScriptPath;
        static string cachedPythonCmd;
        static bool pythonProbed;

        /// <summary>
        /// Resolution order: explicit EditorPrefs override, project-local copy
        /// (LiveCollab_Host/), then this package's Tools~ folder.
        /// </summary>
        public static string LocateScript()
        {
            if (!string.IsNullOrEmpty(cachedScriptPath) && File.Exists(cachedScriptPath))
                return cachedScriptPath;

            string pref = EditorPrefs.GetString("LiveCollab.CompanionPath", "");
            if (!string.IsNullOrEmpty(pref) && File.Exists(pref))
            {
                cachedScriptPath = pref;
                return pref;
            }

            string projCopy = Path.Combine(Directory.GetCurrentDirectory(), ProjectCopyDir, ScriptName);
            if (File.Exists(projCopy))
            {
                cachedScriptPath = projCopy;
                return projCopy;
            }

            string pkg = TryPackagePath();
            if (pkg != null)
            {
                string s = Path.Combine(pkg, "Tools~", ScriptName);
                if (File.Exists(s))
                {
                    cachedScriptPath = s;
                    return s;
                }
            }
            return null;
        }

        public static string LocateExe()
        {
            string overridePath = EditorPrefs.GetString("LiveCollab.CompanionExe", "");
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath))
                return overridePath;

            string projExe = Path.Combine(Directory.GetCurrentDirectory(), ProjectCopyDir, ExeName);
            if (File.Exists(projExe)) return projExe;

            // Unix-style compiled binary (macOS / Linux) — no .exe extension
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                string uni = Path.Combine(Directory.GetCurrentDirectory(), ProjectCopyDir, "livecollab_host");
                if (File.Exists(uni)) return uni;

                string pkgUni = TryPackagePath();
                if (pkgUni != null)
                {
                    string s = Path.Combine(pkgUni, "Tools~", "livecollab_host");
                    if (File.Exists(s)) return s;
                }
            }
            return null;
        }

        static string TryPackagePath()
        {
            try
            {
                var info = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(CompanionHost).Assembly);
                if (info != null) return info.resolvedPath;
            }
            catch (Exception) { }
            string direct = Path.Combine(Directory.GetCurrentDirectory(), "Packages", "com.livecollab.editor");
            return Directory.Exists(direct) ? direct : null;
        }

        public static string FindPython()
        {
            if (pythonProbed) return cachedPythonCmd;
            pythonProbed = true;
            cachedPythonCmd = "";

            string[] candidates = Application.platform == RuntimePlatform.WindowsEditor
                ? new[] { "py -3", "python", "python3" }
                : new[] { "python3", "python" };

            foreach (var cand in candidates)
            {
                if (ProbePython(cand)) { cachedPythonCmd = cand; break; }
            }
            return cachedPythonCmd;
        }

        static bool ProbePython(string cmd)
        {
            try
            {
                var psi = new ProcessStartInfo();
                string[] parts = cmd.Split(' ');
                psi.FileName = parts[0];
                psi.Arguments = (parts.Length > 1 ? parts[1] + " " : "") +
                                "-c \"import sys;print(sys.version_info[0])\"";
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.CreateNoWindow = true;
                using (var p = Process.Start(psi))
                {
                    if (p == null) return false;
                    if (!p.WaitForExit(5000))
                    {
                        try { p.Kill(); } catch (Exception) { }
                        return false;
                    }
                    return p.ExitCode == 0;
                }
            }
            catch (Exception) { return false; }
        }

        /// <summary>Spawn the companion process (EXE if present, else Python).</summary>
        public static bool Spawn(string scriptPath, string pythonCmd, int port, string code,
                                 string name, out Process proc, out string error)
        {
            proc = null;
            error = "";
            try
            {
                string exe = LocateExe();
                var psi = new ProcessStartInfo();

                if (exe != null)
                {
                    psi.FileName = exe;
                    psi.Arguments = string.Format("--port {0} --udp 9595 --code \"{1}\" --name \"{2}\" --quiet",
                        port, code, name);
                }
                else if (!string.IsNullOrEmpty(pythonCmd))
                {
                    string[] parts = pythonCmd.Split(' ');
                    psi.FileName = parts[0];
                    psi.Arguments = (parts.Length > 1 ? parts[1] + " " : "") +
                                    string.Format("\"{0}\" --port {1} --udp 9595 --code \"{2}\" --name \"{3}\" --quiet",
                                        scriptPath, port, code, name);
                }
                else
                {
                    error = "No companion executable found and Python is not installed. " +
                            "Install Python 3 or build LiveCollabHost.exe (Tools~/build_host_exe.bat).";
                    return false;
                }

                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WorkingDirectory = Path.GetDirectoryName(exe != null ? exe : scriptPath);
                proc = Process.Start(psi);
                if (proc == null)
                {
                    error = "Failed to start companion process.";
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                error = "Failed to start companion: " + e.Message;
                return false;
            }
        }

        public static bool WaitForPort(int port, int timeoutMs)
        {
            int waited = 0;
            while (waited < timeoutMs)
            {
                try
                {
                    using (var c = new TcpClient())
                    {
                        var ar = c.BeginConnect("127.0.0.1", port, null, null);
                        if (ar.AsyncWaitHandle.WaitOne(300))
                        {
                            c.EndConnect(ar);
                            return true;
                        }
                    }
                }
                catch (Exception) { }
                Thread.Sleep(250);
                waited += 250;
            }
            return false;
        }

        public static bool IsPortOpen(int port)
        {
            try
            {
                using (var c = new TcpClient())
                {
                    var ar = c.BeginConnect("127.0.0.1", port, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(200)) return false;
                    c.EndConnect(ar);
                    return true;
                }
            }
            catch (Exception) { return false; }
        }
    }
}
