using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Web.Script.Serialization;

namespace PCLOne
{
    internal static class Cfg
    {
        public static string Host = "124.221.231.213";
        public static int JavaPort = 25566;
        public static int BedrockPort = 19132;
        public static string EarthName = "地球服";
        public static string EarthInstance = "Earth-26.3-Fabric";
        public static string EarthGame = @"Earth\.minecraft";
        public static string EarthJava = @"Earth\java\jdk-25.0.5+8\bin\java.exe";
        public static string TitanGame = @"Titan\.minecraft";
        public static string TitanJava = @"Titan\.minecraft\runtime\java-8\bin\java.exe";
        public static string Titan1710 = "1.7.10-Forge10.13.4.1614-1.7.10";
        public static string Titan1122 = "1.12.2-forge-14.23.5.2859";

        public static void Load(string root)
        {
            string path = Path.Combine(root, "pcl-one.json");
            if (!File.Exists(path)) return;
            try
            {
                var ser = new JavaScriptSerializer();
                var doc = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                var earth = doc.ContainsKey("earth") ? doc["earth"] as Dictionary<string, object> : null;
                var titan = doc.ContainsKey("titan") ? doc["titan"] as Dictionary<string, object> : null;
                if (earth != null)
                {
                    if (earth.ContainsKey("host")) Host = Convert.ToString(earth["host"]);
                    if (earth.ContainsKey("javaPort")) JavaPort = Convert.ToInt32(earth["javaPort"]);
                    if (earth.ContainsKey("bedrockPort")) BedrockPort = Convert.ToInt32(earth["bedrockPort"]);
                    if (earth.ContainsKey("instance")) EarthInstance = Convert.ToString(earth["instance"]);
                    if (earth.ContainsKey("name")) EarthName = Convert.ToString(earth["name"]);
                    if (earth.ContainsKey("gameDir")) EarthGame = Convert.ToString(earth["gameDir"]).Replace('/', '\\');
                    if (earth.ContainsKey("java")) EarthJava = Convert.ToString(earth["java"]).Replace('/', '\\');
                }
                if (titan != null)
                {
                    if (titan.ContainsKey("gameDir")) TitanGame = Convert.ToString(titan["gameDir"]).Replace('/', '\\');
                    if (titan.ContainsKey("java")) TitanJava = Convert.ToString(titan["java"]).Replace('/', '\\');
                    if (titan.ContainsKey("version1710")) Titan1710 = Convert.ToString(titan["version1710"]);
                    if (titan.ContainsKey("version1122")) Titan1122 = Convert.ToString(titan["version1122"]);
                }
            }
            catch { }
        }
    }

    internal static class Settings
    {
        public static string Username = "Steve";
        public static int MemoryMb = 4096;

        private static string PathFor(string root) { return Path.Combine(root, "PCLOne-settings.json"); }

        public static void Load(string root)
        {
            try
            {
                string path = PathFor(root);
                if (!File.Exists(path)) return;
                var ser = new JavaScriptSerializer();
                var doc = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                if (doc.ContainsKey("username")) Username = Convert.ToString(doc["username"]);
                if (doc.ContainsKey("memoryMb")) MemoryMb = Convert.ToInt32(doc["memoryMb"]);
            }
            catch { }
        }

        public static void Save(string root)
        {
            try
            {
                var ser = new JavaScriptSerializer();
                var doc = new Dictionary<string, object>();
                doc["username"] = Username;
                doc["memoryMb"] = MemoryMb;
                File.WriteAllText(PathFor(root), ser.Serialize(doc), new UTF8Encoding(false));
            }
            catch { }
        }

        public static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Steve";
            var sb = new StringBuilder();
            foreach (char c in value.Trim())
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '_') sb.Append(c);
            string result = sb.ToString();
            if (result.Length < 3) result = "P_" + result;
            if (result.Length > 16) result = result.Substring(0, 16);
            return result.Length == 0 ? "Steve" : result;
        }
    }

    /// <summary>
    /// Windowless entry point. No launcher UI, no dialogs: arguments pick the feature,
    /// the engine starts java, the process exits. Errors go to PCLOne-error.txt.
    /// </summary>
    internal static class Program
    {
        internal static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;

        internal static string Pick(string packaged, string fallback)
        {
            if (File.Exists(packaged) || Directory.Exists(packaged)) return packaged;
            if (File.Exists(fallback) || Directory.Exists(fallback)) return fallback;
            return packaged;
        }

        internal static string EarthRoot { get { return Pick(Path.Combine(Root, Cfg.EarthGame), Path.Combine(Root, @"vendor\earth\game")); } }
        internal static string EarthJavaExe { get { return Pick(Path.Combine(Root, Cfg.EarthJava), Path.Combine(Root, @"vendor\pcl-pro\PCLProRuntime\PCL\runtime\jdk-25.0.5+8\bin\java.exe")); } }
        internal static string TitanRoot { get { return Pick(Path.Combine(Root, Cfg.TitanGame), Path.Combine(Root, @"vendor\titan\.minecraft")); } }
        internal static string TitanJavaExe { get { return Pick(Path.Combine(Root, Cfg.TitanJava), Path.Combine(Root, @"vendor\titan\.minecraft\runtime\java-8\bin\java.exe")); } }
        internal static string ServerBat { get { return Pick(Path.Combine(Root, @"Server\start.bat"), Path.Combine(Root, @"vendor\server\start.bat")); } }
        internal static string ServerDir { get { return Path.GetDirectoryName(ServerBat); } }

        internal static LaunchRequest RequestFor(string kind)
        {
            switch (kind)
            {
                case "earth": return EarthRequest(true);
                case "earth-local":
                case "earthlocal":
                case "single": return EarthRequest(false);
                case "titan1710":
                case "1710":
                    return new LaunchRequest
                    {
                        Display = "泰坦 1.7.10", GameRoot = TitanRoot, Instance = Cfg.Titan1710,
                        Java = TitanJavaExe, MemoryMb = Settings.MemoryMb,
                        Username = Settings.Sanitize(Settings.Username), JoinServer = ""
                    };
                case "titan1122":
                case "1122":
                    return new LaunchRequest
                    {
                        Display = "泰坦 1.12.2", GameRoot = TitanRoot, Instance = Cfg.Titan1122,
                        Java = TitanJavaExe, MemoryMb = Settings.MemoryMb,
                        Username = Settings.Sanitize(Settings.Username), JoinServer = ""
                    };
                default: return null;
            }
        }

        private static LaunchRequest EarthRequest(bool join)
        {
            return new LaunchRequest
            {
                Display = Cfg.EarthName,
                GameRoot = EarthRoot,
                Instance = Cfg.EarthInstance,
                Java = EarthJavaExe,
                MemoryMb = Settings.MemoryMb,
                Username = Settings.Sanitize(Settings.Username),
                JoinServer = join ? Cfg.Host + ":" + Cfg.JavaPort : ""
            };
        }

        internal static bool Online(int timeoutMs)
        {
            try
            {
                using (var client = new TcpClient())
                {
                    var ar = client.BeginConnect(Cfg.Host, Cfg.JavaPort, null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) return false;
                    try { client.EndConnect(ar); return true; } catch { return false; }
                }
            }
            catch { return false; }
        }

        private static int Main(string[] args)
        {
            Cfg.Load(Root);
            Settings.Load(Root);
            string command = args.Length > 0 ? args[0].ToLowerInvariant().TrimStart('-') : "earth";
            string extra = args.Length > 1 ? args[1].ToLowerInvariant() : null;

            try
            {
                switch (command)
                {
                    case "earth":
                    case "enter":
                        return RunGame("earth");
                    case "earth-local":
                    case "earthlocal":
                    case "single":
                        return RunGame("earth-local");
                    case "1710":
                    case "titan1710":
                        return RunGame("titan1710");
                    case "1122":
                    case "titan1122":
                        return RunGame("titan1122");
                    case "launch":
                        return RunGeneric(extra, args.Length > 2 ? args[2] : null, args.Length > 3 ? args[3] : null);
                    case "versions":
                        return ListVersions();
                    case "plan":
                        return PlanCommand(extra ?? "earth");
                    case "server":
                        return RunServer();
                    case "selftest":
                        return SelfTest();
                    case "version":
                        WriteState("version", "ok", "PCL One launcher");
                        return 0;
                    default:
                        return Fail("unknown command: " + command);
                }
            }
            catch (Exception ex)
            {
                return Fail(ex.ToString());
            }
        }

        private static int RunGeneric(string gameRel, string instance, string server)
        {
            if (string.IsNullOrEmpty(gameRel) || string.IsNullOrEmpty(instance))
                return Fail("usage: launch <gameDirRelative> <instance> [server]");

            string gameRoot = Path.Combine(Root, gameRel.Replace('/', '\\'));
            if (!Directory.Exists(gameRoot)) return Fail("game dir not found: " + gameRoot);

            string java = FindJava(gameRoot);
            if (java == null) return Fail("no java found for " + gameRoot);

            var req = new LaunchRequest
            {
                Display = instance,
                GameRoot = gameRoot,
                Instance = instance,
                Java = java,
                MemoryMb = Settings.MemoryMb,
                Username = Settings.Sanitize(Settings.Username),
                JoinServer = server ?? ""
            };

            if (!string.IsNullOrEmpty(req.JoinServer) && !Online(1200))
            {
                req.JoinServer = "";
                WriteState("launch", "offline-fallback", "server unreachable: " + gameRoot + " " + instance);
            }

            var plan = LaunchPlan.Build(req, true);
            if (!plan.Ok) return Fail("launch prerequisites:\r\n" + string.Join("\r\n", plan.Errors.ToArray()));

            string log = Path.Combine(plan.WorkingDirectory, "PCLOne-launch.log");
            try { if (File.Exists(log)) File.Delete(log); } catch { }
            var proc = plan.Start(log);
            WriteState("launch", "started",
                "game=" + gameRel + " instance=" + instance + " server=" + (server ?? "") +
                " pid=" + proc.Id + " classpath=" + plan.ClasspathEntries + " java=" + java);
            return 0;
        }

        /// <summary>Finds the nearest bundled Java for an arbitrary game folder.</summary>
        private static string FindJava(string gameRoot)
        {
            try
            {
                string runtime = Path.Combine(gameRoot, "runtime");
                if (Directory.Exists(runtime))
                {
                    var dirs = Directory.GetDirectories(runtime);
                    Array.Sort(dirs);
                    foreach (var dir in dirs)
                    {
                        string exe = Path.Combine(dir, @"bin\java.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }

                var parent = Directory.GetParent(gameRoot);
                if (parent != null)
                {
                    string javaRoot = Path.Combine(parent.FullName, "java");
                    if (Directory.Exists(javaRoot))
                    {
                        var dirs = Directory.GetDirectories(javaRoot);
                        Array.Sort(dirs);
                        foreach (var dir in dirs)
                        {
                            string exe = Path.Combine(dir, @"bin\java.exe");
                            if (File.Exists(exe)) return exe;
                        }
                    }
                }

                string localJava = Path.Combine(gameRoot, "java");
                if (Directory.Exists(localJava))
                {
                    var dirs = Directory.GetDirectories(localJava);
                    Array.Sort(dirs);
                    foreach (var dir in dirs)
                    {
                        string exe = Path.Combine(dir, @"bin\java.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }

                // source-tree fallbacks (vendor layout)
                string lower = gameRoot.ToLowerInvariant();
                if (lower.Contains("titan"))
                {
                    string titanJava = Path.Combine(Root, @"vendor\titan\.minecraft\runtime\java-8\bin\java.exe");
                    if (File.Exists(titanJava)) return titanJava;
                }
                string vendorJdkRoot = Path.Combine(Root, @"vendor\pcl-pro\PCLProRuntime\PCL\runtime");
                if (Directory.Exists(vendorJdkRoot))
                {
                    foreach (var dir in Directory.GetDirectories(vendorJdkRoot))
                    {
                        string exe = Path.Combine(dir, @"bin\java.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }

                // last resort: any java under the package
                foreach (var dir in Directory.GetDirectories(Root))
                {
                    string candidate = Path.Combine(dir, @"java");
                    if (!Directory.Exists(candidate)) continue;
                    foreach (var jdk in Directory.GetDirectories(candidate))
                    {
                        string exe = Path.Combine(jdk, @"bin\java.exe");
                        if (File.Exists(exe)) return exe;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>Writes every version found under the package's game folders (for the in-game menu).</summary>
        private static int ListVersions()
        {
            var lines = new List<string>();
            var roots = new List<string>();
            roots.Add(Path.Combine(Root, Cfg.EarthGame));
            roots.Add(Path.Combine(Root, Cfg.TitanGame));
            roots.Add(Path.Combine(Root, @"vendor\earth\game"));
            roots.Add(Path.Combine(Root, @"vendor\titan\.minecraft"));
            try
            {
                foreach (var dir in Directory.GetDirectories(Root))
                {
                    string mc = Path.Combine(dir, ".minecraft");
                    if (Directory.Exists(Path.Combine(mc, "versions")) && !roots.Contains(mc)) roots.Add(mc);
                }
            }
            catch { }

            foreach (var gameRoot in roots)
            {
                string versions = Path.Combine(gameRoot, "versions");
                if (!Directory.Exists(versions)) continue;
                string java = FindJava(gameRoot) ?? "";
                foreach (var instanceDir in Directory.GetDirectories(versions))
                {
                    string id = Path.GetFileName(instanceDir);
                    if (!File.Exists(Path.Combine(instanceDir, id + ".json"))) continue;
                    string rel = gameRoot.Substring(Root.Length).TrimStart('\\').TrimEnd('\\');
                    lines.Add(rel + "\t" + id + "\t" + java);
                }
            }

            File.WriteAllLines(Path.Combine(Root, "PCLOne-versions.txt"), lines.ToArray(), new UTF8Encoding(false));
            WriteState("versions", "ok", lines.Count + " versions");
            return 0;
        }

        private static int RunGame(string kind)
        {
            var req = RequestFor(kind);
            if (req == null) return Fail("unknown target: " + kind);

            if (!string.IsNullOrEmpty(req.JoinServer) && !Online(1200))
            {
                req.JoinServer = "";
                WriteState(kind, "offline-fallback", "server unreachable, launching single player");
            }

            var plan = LaunchPlan.Build(req, true);
            if (!plan.Ok) return Fail("launch prerequisites:\r\n" + string.Join("\r\n", plan.Errors.ToArray()));

            string log = Path.Combine(plan.WorkingDirectory, "PCLOne-launch.log");
            try { if (File.Exists(log)) File.Delete(log); } catch { }
            var proc = plan.Start(log);
            WriteState(kind, "started", "pid=" + proc.Id + " classpath=" + plan.ClasspathEntries + " java=" + req.Java);
            return 0;
        }

        private static int RunServer()
        {
            if (!File.Exists(ServerBat)) return Fail("missing server script: " + ServerBat);
            var psi = new ProcessStartInfo(ServerBat, "");
            psi.WorkingDirectory = ServerDir;
            psi.UseShellExecute = true;
            Process.Start(psi);
            WriteState("server", "started", ServerBat);
            return 0;
        }

        private static int PlanCommand(string kind)
        {
            var req = RequestFor(kind);
            if (req == null) return Fail("unknown target: " + kind);
            if (!string.IsNullOrEmpty(req.JoinServer) && !Online(1200)) req.JoinServer = "";

            var plan = LaunchPlan.Build(req, false);
            var sb = new StringBuilder();
            sb.AppendLine("target\t" + kind);
            sb.AppendLine("instance\t" + req.Instance);
            sb.AppendLine("game\t" + req.GameRoot);
            sb.AppendLine("java\t" + req.Java);
            sb.AppendLine("main\t" + plan.MainClass);
            sb.AppendLine("classpath\t" + plan.ClasspathEntries);
            foreach (var f in plan.Facts) sb.AppendLine("fact\t" + f);
            foreach (var e in plan.Errors) sb.AppendLine("error\t" + e);
            File.WriteAllText(Path.Combine(Root, "PCLOne-plan.txt"), sb.ToString(), new UTF8Encoding(false));

            if (!plan.Ok) return Fail("plan failed: " + string.Join("; ", plan.Errors.ToArray()));
            WriteState("plan", "ok", kind + " classpath=" + plan.ClasspathEntries);
            return 0;
        }

        internal static int SelfTest()
        {
            var lines = new List<string>();
            int bad = 0;
            Action<string, bool, string> check = delegate (string label, bool ok, string detail)
            {
                lines.Add(label + "\t" + (ok ? "OK" : "MISSING") + "\t" + detail);
                if (!ok) bad++;
            };

            check("Shared config", File.Exists(Path.Combine(Root, "pcl-one.json")), Path.Combine(Root, "pcl-one.json"));
            check("Readme", File.Exists(Path.Combine(Root, "README.md")), Path.Combine(Root, "README.md"));

            foreach (var kind in new string[] { "earth", "titan1710", "titan1122" })
            {
                var req = RequestFor(kind);
                if (req == null) { check("plan " + kind, false, "unknown target"); continue; }
                var plan = LaunchPlan.Build(req, false);
                check("plan " + kind, plan.Ok, plan.Ok
                    ? (plan.ClasspathEntries + " jars, main=" + plan.MainClass + ", java=" + req.Java)
                    : string.Join("; ", plan.Errors.ToArray()));

                string indexId = AssetIndexOf(req);
                string indexPath = Path.Combine(req.GameRoot, "assets", "indexes", indexId + ".json");
                int assetTotal = 0;
                int assetMissing = 0;
                if (File.Exists(indexPath))
                {
                    CheckAssetObjects(req, indexPath, out assetTotal, out assetMissing);
                    check("assets " + kind, assetMissing == 0,
                        assetMissing == 0
                            ? (assetTotal + " objects, index " + indexId)
                            : (assetMissing + " of " + assetTotal + " objects missing, index " + indexId));
                }
                else
                {
                    check("assets " + kind, false, indexPath);
                }
            }

            check("Earth mods (8)", CountJars(Path.Combine(EarthRoot, @"versions\" + Cfg.EarthInstance + @"\mods")) >= 8,
                Path.Combine(EarthRoot, @"versions\" + Cfg.EarthInstance + @"\mods"));
            string menuMods = Path.Combine(EarthRoot, @"versions\" + Cfg.EarthInstance + @"\mods");
            bool hasMenu = Directory.Exists(menuMods) && Directory.GetFiles(menuMods, "pclone-menu-*.jar").Length > 0;
            check("In-game menu mod", hasMenu, menuMods + "\\pclone-menu-*.jar");
            check("Titan 1.7.10 mods", CountJars(Path.Combine(TitanRoot, @"versions\" + Cfg.Titan1710 + @"\mods")) >= 5,
                Path.Combine(TitanRoot, @"versions\" + Cfg.Titan1710 + @"\mods"));
            check("Titan 1.12.2 mods", CountJars(Path.Combine(TitanRoot, @"versions\" + Cfg.Titan1122 + @"\mods")) >= 1,
                Path.Combine(TitanRoot, @"versions\" + Cfg.Titan1122 + @"\mods"));

            check("Server start.bat", File.Exists(ServerBat), ServerBat);
            check("Server forge jar", File.Exists(Path.Combine(ServerDir, "forge-1.7.10-10.13.4.1614-1.7.10-universal.jar")),
                Path.Combine(ServerDir, "forge-1.7.10-10.13.4.1614-1.7.10-universal.jar"));
            check("Server world", File.Exists(Path.Combine(ServerDir, @"world\level.dat")), Path.Combine(ServerDir, @"world\level.dat"));
            check("Java 25 (Earth)", File.Exists(EarthJavaExe), EarthJavaExe);
            check("Java 8 (Titan)", File.Exists(TitanJavaExe), TitanJavaExe);
            check("No launcher UI", !Directory.Exists(Path.Combine(Root, @"Earth\PCLPro")) &&
                !File.Exists(Path.Combine(Root, @"Titan\Plain Craft Launcher 2.exe")), "PCLPro/PCL2 absent");

            string[] shortcuts = { "进入地球.lnk", "地球单机.lnk", "泰坦 1.7.10.lnk", "泰坦 1.12.2.lnk", "开泰坦服.lnk", "离线自检.lnk" };
            bool allShortcuts = true;
            foreach (var lnk in shortcuts) if (!File.Exists(Path.Combine(Root, lnk))) allShortcuts = false;
            check("Shortcuts (6)", allShortcuts, Root + "\\*.lnk");
            check("Server list", File.Exists(Path.Combine(Root, "servers.txt")), Path.Combine(Root, "servers.txt"));

            int consoleScripts = 0;
            try
            {
                foreach (var bat in Directory.GetFiles(Root, "*.bat", SearchOption.AllDirectories))
                {
                    string full = Path.GetFullPath(bat);
                    if (full.StartsWith(Path.GetFullPath(ServerDir), StringComparison.OrdinalIgnoreCase)) continue;
                    consoleScripts++;
                }
            }
            catch { consoleScripts = 1; }
            check("No console UIs", consoleScripts == 0, consoleScripts == 0 ? "only Server\\start.bat" : (consoleScripts + " stray .bat"));

            string report = Path.Combine(Root, "PCLOne-selftest.txt");
            File.WriteAllLines(report, lines.ToArray(), new UTF8Encoding(false));
            WriteState("selftest", bad == 0 ? "ok" : "failed", report);
            return bad == 0 ? 0 : 1;
        }

        private static void CheckAssetObjects(LaunchRequest req, string indexPath, out int total, out int missing)
        {
            total = 0;
            missing = 0;
            try
            {
                var ser = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
                var doc = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(indexPath, Encoding.UTF8));
                var objects = doc["objects"] as Dictionary<string, object>;
                if (objects == null) return;
                string objectsRoot = Path.Combine(req.GameRoot, "assets", "objects");
                foreach (var pair in objects)
                {
                    total++;
                    var meta = pair.Value as Dictionary<string, object>;
                    if (meta == null || !meta.ContainsKey("hash")) { missing++; continue; }
                    string hash = Convert.ToString(meta["hash"]);
                    if (hash.Length < 2) { missing++; continue; }
                    if (!File.Exists(Path.Combine(objectsRoot, hash.Substring(0, 2), hash))) missing++;
                }
            }
            catch { missing = Math.Max(missing, 1); }
        }

        private static string AssetIndexOf(LaunchRequest req)
        {
            try
            {
                var ser = new JavaScriptSerializer();
                var doc = ser.Deserialize<Dictionary<string, object>>(
                    File.ReadAllText(Path.Combine(req.GameRoot, "versions", req.Instance, req.Instance + ".json"), Encoding.UTF8));
                object ai;
                if (doc.TryGetValue("assetIndex", out ai))
                {
                    var d = ai as Dictionary<string, object>;
                    if (d != null && d.ContainsKey("id")) return Convert.ToString(d["id"]);
                }
                object parent;
                if (doc.TryGetValue("inheritsFrom", out parent))
                {
                    var p = Convert.ToString(parent);
                    var pd = ser.Deserialize<Dictionary<string, object>>(
                        File.ReadAllText(Path.Combine(req.GameRoot, "versions", p, p + ".json"), Encoding.UTF8));
                    var d = pd["assetIndex"] as Dictionary<string, object>;
                    if (d != null && d.ContainsKey("id")) return Convert.ToString(d["id"]);
                }
            }
            catch { }
            return "?";
        }

        private static int CountJars(string dir)
        {
            try { return Directory.Exists(dir) ? Directory.GetFiles(dir, "*.jar").Length : 0; }
            catch { return 0; }
        }

        private static int Fail(string message)
        {
            try
            {
                File.WriteAllText(Path.Combine(Root, "PCLOne-error.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + message, new UTF8Encoding(false));
            }
            catch { }
            WriteState("error", "failed", message);
            return 3;
        }

        private static void WriteState(string command, string status, string detail)
        {
            try
            {
                File.WriteAllText(Path.Combine(Root, "PCLOne-last.txt"),
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" +
                    "command=" + command + "\r\nstatus=" + status + "\r\n" + detail, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
