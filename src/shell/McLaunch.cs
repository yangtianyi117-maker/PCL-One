using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace PCLOne
{
    /// <summary>Everything needed to turn a version folder into a java process.</summary>
    internal sealed class LaunchRequest
    {
        public string Display = "";
        public string GameRoot = "";      // folder containing versions/, libraries/, assets/
        public string Instance = "";      // version id
        public string Java = "";          // java.exe
        public int MemoryMb = 4096;
        public string Username = "Steve";
        public string JoinServer = "";    // host:port, optional
    }

    /// <summary>
    /// Picks a Java for a version by reading javaVersion.majorVersion from the version JSON
    /// (following inheritsFrom) and matching the bundled JDKs. No hardcoded per-instance Java.
    /// </summary>
    internal static class JavaResolver
    {
        public static string Choose(string packageRoot, string gameRoot, string instanceId)
        {
            int required = RequiredMajor(gameRoot, instanceId);
            var candidates = new List<string>();

            AddRuntime(candidates, Path.Combine(gameRoot, "runtime"));
            var parent = Directory.GetParent(gameRoot);
            if (parent != null) AddRuntime(candidates, Path.Combine(parent.FullName, "java"));
            AddRuntime(candidates, Path.Combine(gameRoot, "java"));

            try
            {
                foreach (var dir in Directory.GetDirectories(packageRoot))
                {
                    AddRuntime(candidates, Path.Combine(dir, "java"));
                    AddRuntime(candidates, Path.Combine(dir, ".minecraft", "runtime"));
                }
            }
            catch { }

            AddRuntime(candidates, Path.Combine(packageRoot, @"vendor\pcl-pro\PCLProRuntime\PCL\runtime"));
            AddRuntime(candidates, Path.Combine(packageRoot, @"vendor\titan\.minecraft\runtime"));

            string exact = null, higher = null, lower = null;
            int higherMajor = int.MaxValue, lowerMajor = int.MinValue;

            foreach (var java in candidates)
            {
                int major;
                try { major = LaunchPlan.JavaMajor(java); } catch { continue; }

                if (major == required) { exact = java; break; }
                if (major > required && major < higherMajor) { higher = java; higherMajor = major; }
                if (major < required && major > lowerMajor) { lower = java; lowerMajor = major; }
            }

            return exact ?? higher ?? lower;
        }

        private static void AddRuntime(List<string> list, string runtimeRoot)
        {
            try
            {
                if (!Directory.Exists(runtimeRoot)) return;

                string direct = Path.Combine(runtimeRoot, @"bin\java.exe");
                if (File.Exists(direct) && !list.Contains(direct)) list.Add(direct);

                foreach (var dir in Directory.GetDirectories(runtimeRoot))
                {
                    string exe = Path.Combine(dir, @"bin\java.exe");
                    if (File.Exists(exe) && !list.Contains(exe)) list.Add(exe);
                }
            }
            catch { }
        }

        private static int RequiredMajor(string gameRoot, string instanceId)
        {
            try
            {
                var ser = new JavaScriptSerializer();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                string id = instanceId;

                while (!string.IsNullOrEmpty(id) && seen.Add(id))
                {
                    string path = Path.Combine(gameRoot, "versions", id, id + ".json");
                    if (!File.Exists(path)) break;

                    var doc = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                    object javaVersion;
                    if (doc.TryGetValue("javaVersion", out javaVersion))
                    {
                        var jv = javaVersion as Dictionary<string, object>;
                        object major;
                        if (jv != null && jv.TryGetValue("majorVersion", out major))
                            return Convert.ToInt32(major);
                    }

                    object parent;
                    id = doc.TryGetValue("inheritsFrom", out parent) ? Convert.ToString(parent) : null;
                }
            }
            catch { }
            return 8;
        }
    }

    /// <summary>
    /// First-principles launcher: read version JSON (+ inheritsFrom chain), resolve libraries,
    /// extract natives, build the java command line. No third-party launcher involved.
    /// </summary>
    internal sealed class LaunchPlan
    {
        public string Java = "";
        public string Arguments = "";
        public string WorkingDirectory = "";
        public string NativesDirectory = "";
        public string MainClass = "";
        public int ClasspathEntries;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Facts = new List<string>();

        public bool Ok { get { return Errors.Count == 0; } }

        private static readonly Dictionary<string, int> JavaMajorCache = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public static int JavaMajor(string java)
        {
            lock (JavaMajorCache)
            {
                int cached;
                if (JavaMajorCache.TryGetValue(java, out cached)) return cached;
            }

            int major = 8;
            try
            {
                var psi = new ProcessStartInfo(java, "-version");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (var p = Process.Start(psi))
                {
                    string text = p.StandardError.ReadToEnd() + p.StandardOutput.ReadToEnd();
                    p.WaitForExit(8000);
                    var m = Regex.Match(text, "version \"(?:1\\.)?(\\d+)");
                    if (m.Success) major = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                }
            }
            catch { }

            lock (JavaMajorCache) { JavaMajorCache[java] = major; }
            return major;
        }

        public static LaunchPlan Build(LaunchRequest req, bool extractNatives)
        {
            var plan = new LaunchPlan();
            plan.WorkingDirectory = Path.Combine(req.GameRoot, "versions", req.Instance);

            if (string.IsNullOrEmpty(req.Java))
                req.Java = JavaResolver.Choose(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'), req.GameRoot, req.Instance) ?? "";
            plan.Java = req.Java;

            if (string.IsNullOrEmpty(req.Java)) plan.Errors.Add("找不到匹配的 Java（版本要求 " + req.Instance + "）");
            else if (!File.Exists(req.Java)) plan.Errors.Add("Java 不存在：" + req.Java);
            if (!Directory.Exists(plan.WorkingDirectory)) { plan.Errors.Add("实例目录不存在：" + plan.WorkingDirectory); return plan; }

            string instanceJson = Path.Combine(plan.WorkingDirectory, req.Instance + ".json");
            if (!File.Exists(instanceJson)) { plan.Errors.Add("版本 JSON 不存在：" + instanceJson); return plan; }

            var ser = new JavaScriptSerializer();
            ser.MaxJsonLength = int.MaxValue;
            var chain = new List<Dictionary<string, object>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string currentId = req.Instance;
            Dictionary<string, object> current = null;
            while (!string.IsNullOrEmpty(currentId) && seen.Add(currentId))
            {
                string path = Path.Combine(req.GameRoot, "versions", currentId, currentId + ".json");
                if (!File.Exists(path))
                {
                    plan.Errors.Add("缺少继承版本 JSON：" + path);
                    break;
                }
                current = ser.Deserialize<Dictionary<string, object>>(File.ReadAllText(path, Encoding.UTF8));
                chain.Add(current);
                currentId = Str(current, "inheritsFrom");
            }

            if (chain.Count == 0) return plan;

            // ---- merged scalars -------------------------------------------------
            string mainClass = FirstNonEmpty(chain, "mainClass");
            string minecraftArguments = FirstNonEmpty(chain, "minecraftArguments");
            string assetIndex = null;
            string jarId = null;
            DateTime releaseTime = DateTime.MinValue;

            foreach (var node in chain)
            {
                if (assetIndex == null)
                {
                    var ai = Obj(node, "assetIndex");
                    if (ai != null) assetIndex = Str(ai, "id");
                }
                if (jarId == null) jarId = Str(node, "jar");
                string rt = Str(node, "releaseTime");
                DateTime parsed;
                if (rt != null && DateTime.TryParse(rt, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed) && parsed > releaseTime)
                    releaseTime = parsed;
            }
            if (assetIndex == null) plan.Errors.Add("版本 JSON 没有 assetIndex");
            if (jarId == null) jarId = chain[chain.Count - 1]["id"] != null ? Str(chain[chain.Count - 1], "id") : req.Instance;
            if (mainClass == null) plan.Errors.Add("版本 JSON 没有 mainClass");
            plan.MainClass = mainClass ?? "";

            // ---- libraries: child overrides parent by group:artifact ------------
            var chosen = new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < chain.Count; i++)
            {
                var libs = Arr(chain[i], "libraries");
                if (libs == null) continue;
                foreach (var raw in libs)
                {
                    var lib = raw as Dictionary<string, object>;
                    if (lib == null) continue;
                    string name = Str(lib, "name");
                    if (string.IsNullOrEmpty(name)) continue;
                    string key = ArtifactKey(name);
                    if (!chosen.ContainsKey(key)) chosen[key] = lib; // child wins
                }
            }

            var orderedLibs = new List<Dictionary<string, object>>();
            for (int i = chain.Count - 1; i >= 0; i--) // parent first
            {
                var libs = Arr(chain[i], "libraries");
                if (libs == null) continue;
                foreach (var raw in libs)
                {
                    var lib = raw as Dictionary<string, object>;
                    if (lib == null) continue;
                    string name = Str(lib, "name");
                    if (string.IsNullOrEmpty(name)) continue;
                    string key = ArtifactKey(name);
                    Dictionary<string, object> picked;
                    if (chosen.TryGetValue(key, out picked) && ReferenceEquals(picked, lib))
                        orderedLibs.Add(lib);
                }
            }

            string librariesRoot = Path.Combine(req.GameRoot, "libraries");
            var classpath = new List<string>();
            var nativesJars = new List<string>();
            plan.NativesDirectory = Path.Combine(plan.WorkingDirectory, "PCLOne-natives");

            foreach (var lib in orderedLibs)
            {
                if (!RulesOk(lib)) continue;
                string name = Str(lib, "name");

                var natives = Obj(lib, "natives");
                if (natives != null)
                {
                    string win = Str(natives, "windows");
                    if (win == null) continue; // this library is not for Windows
                    string classifier = win.Replace("${arch}", "64");
                    string nativePath = ClassifierFile(lib, name, classifier, librariesRoot);
                    if (nativePath == null)
                        plan.Errors.Add("缺少 natives 分类器：" + name + " -> " + classifier);
                    else
                        nativesJars.Add(nativePath);
                }

                string artifact = ArtifactFile(lib, name, librariesRoot);
                if (artifact != null)
                {
                    classpath.Add(artifact);
                    if (artifact.IndexOf("natives-windows", StringComparison.OrdinalIgnoreCase) >= 0)
                        nativesJars.Add(artifact);
                }
                else if (natives == null)
                    plan.Errors.Add("缺少库文件：" + name);
            }

            string mainJar = Path.Combine(req.GameRoot, "versions", jarId, jarId + ".jar");
            if (File.Exists(mainJar)) classpath.Add(mainJar);
            else plan.Errors.Add("缺少客户端主 jar：" + mainJar);

            plan.ClasspathEntries = classpath.Count;

            // ---- natives extraction --------------------------------------------
            if (extractNatives && nativesJars.Count > 0)
            {
                try
                {
                    if (Directory.Exists(plan.NativesDirectory)) Directory.Delete(plan.NativesDirectory, true);
                    Directory.CreateDirectory(plan.NativesDirectory);
                    foreach (var jar in nativesJars) ExtractNatives(jar, plan.NativesDirectory);
                    plan.Facts.Add("natives: " + nativesJars.Count + " jar(s) -> " + plan.NativesDirectory);
                }
                catch (Exception ex)
                {
                    plan.Errors.Add("解压 natives 失败：" + ex.Message);
                }
            }

            // ---- game arguments --------------------------------------------------
            var gameArgs = new List<string>();
            if (!string.IsNullOrEmpty(minecraftArguments))
            {
                foreach (var token in SplitArguments(minecraftArguments)) gameArgs.Add(token);
            }
            else
            {
                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    var arguments = Obj(chain[i], "arguments");
                    if (arguments == null) continue;
                    var game = Arr(arguments, "game");
                    if (game == null) continue;
                    foreach (var raw in game)
                    {
                        string text = raw as string;
                        if (text != null) { gameArgs.Add(text); continue; }
                        var ruleObj = raw as Dictionary<string, object>;
                        if (ruleObj == null) continue;
                        if (!RulesOk(ruleObj)) continue;
                        var value = ruleObj.ContainsKey("value") ? ruleObj["value"] : null;
                        if (value is string) gameArgs.Add((string)value);
                        else
                        {
                            var list = value as System.Collections.IEnumerable;
                            if (list != null && !(value is string))
                                foreach (var v in list)
                                    gameArgs.Add(Convert.ToString(v, CultureInfo.InvariantCulture));
                        }
                    }
                }
            }

            string username = string.IsNullOrEmpty(req.Username) ? "Steve" : req.Username.Trim();
            string uuid = OfflineUuid(username);
            string accessToken = OfflineToken(username);

            var tokens = new Dictionary<string, string>
            {
                { "${auth_player_name}", username },
                { "${version_name}", req.Instance },
                { "${game_directory}", plan.WorkingDirectory },
                { "${assets_root}", Path.Combine(req.GameRoot, "assets") },
                { "${game_assets}", Path.Combine(req.GameRoot, "assets", "virtual", "legacy") },
                { "${assets_index_name}", assetIndex ?? "" },
                { "${auth_uuid}", uuid },
                { "${auth_access_token}", accessToken },
                { "${access_token}", accessToken },
                { "${auth_session}", accessToken },
                { "${clientid}", "pclone" },
                { "${auth_xuid}", "" },
                { "${user_type}", "legacy" },
                { "${version_type}", "PCLOne" },
                { "${user_properties}", "{}" },
                { "${natives_directory}", plan.NativesDirectory },
                { "${launcher_name}", "PCLOne" },
                { "${launcher_version}", "3" },
                { "${classpath}", string.Join(";", classpath.ToArray()) },
                { "${classpath_separator}", ";" },
                { "${library_directory}", librariesRoot },
                { "${libraries_directory}", librariesRoot },
                { "${resolution_width}", "854" },
                { "${resolution_height}", "480" },
            };

            var expanded = new List<string>();
            foreach (var arg in gameArgs) expanded.Add(Expand(arg, tokens));

            // Drop "--flag <empty>" pairs (e.g. --xuid with offline auth) and bare empties.
            var finalArgs = new List<string>();
            for (int i = 0; i < expanded.Count; i++)
            {
                string argValue = expanded[i];
                if (argValue.Length == 0) continue;
                if (argValue.StartsWith("-") && i + 1 < expanded.Count && expanded[i + 1].Length == 0)
                {
                    i++;
                    continue;
                }
                finalArgs.Add(argValue);
            }
            expanded = finalArgs;

            if (!string.IsNullOrEmpty(req.JoinServer))
            {
                if (releaseTime > new DateTime(2023, 4, 4))
                {
                    expanded.Add("--quickPlayMultiplayer");
                    expanded.Add("\"" + req.JoinServer + "\"");
                }
                else
                {
                    string[] hp = req.JoinServer.Split(':');
                    expanded.Add("--server");
                    expanded.Add(hp[0]);
                    expanded.Add("--port");
                    expanded.Add(hp.Length > 1 ? hp[1] : "25565");
                }
            }

            // ---- JVM arguments ---------------------------------------------------
            int major = JavaMajor(req.Java);
            var jvm = new List<string>();
            jvm.Add("-Xmx" + Math.Max(512, req.MemoryMb) + "M");
            jvm.Add("-Xms512M");
            jvm.Add("-XX:+UseG1GC");
            jvm.Add("-Djava.library.path=" + Quote(plan.NativesDirectory));
            jvm.Add("-Dminecraft.launcher.brand=PCLOne");
            jvm.Add("-Dminecraft.launcher.version=3");
            jvm.Add("-Dlog4j2.formatMsgNoLookups=true");
            if (major >= 9)
            {
                jvm.Add("-Dfile.encoding=COMPAT");
                jvm.Add("-Dstdout.encoding=UTF-8");
                jvm.Add("-Dstderr.encoding=UTF-8");
                jvm.Add("--enable-native-access=ALL-UNNAMED");
                jvm.Add("--add-exports");
                jvm.Add("java.base/jdk.internal.misc=ALL-UNNAMED");
            }
            jvm.Add("-cp");
            jvm.Add("\"" + string.Join(";", classpath.ToArray()) + "\"");

            var sb = new StringBuilder();
            foreach (var a in jvm) sb.Append(a).Append(' ');
            sb.Append(mainClass).Append(' ');
            foreach (var a in expanded) sb.Append(a).Append(' ');

            plan.Arguments = sb.ToString().TrimEnd();
            plan.Facts.Add("java: " + req.Java + " (major " + major + ")");
            plan.Facts.Add("classpath: " + classpath.Count + " jar(s)");
            plan.Facts.Add("instance: " + req.Instance + " @ " + plan.WorkingDirectory);
            plan.Facts.Add("assets: " + (assetIndex ?? "?"));
            return plan;
        }

        public Process Start(string logPath)
        {
            if (!Ok) throw new InvalidOperationException(string.Join("\r\n", Errors.ToArray()));

            // The launcher exits immediately; do not tie the game's stdio to our lifetime.
            // Minecraft writes its own logs/<date>.log, which is the real log.
            var psi = new ProcessStartInfo(Java, Arguments);
            psi.WorkingDirectory = WorkingDirectory;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            var proc = Process.Start(psi);
            if (proc == null) throw new InvalidOperationException("无法启动 java 进程");
            return proc;
        }

        private static string Quote(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            return value.IndexOf(' ') >= 0 && !value.StartsWith("\"") ? "\"" + value + "\"" : value;
        }

        private static readonly object LogLock = new object();

        private static void AppendLog(string path, string line)
        {
            if (line == null) return;
            try
            {
                lock (LogLock)
                {
                    File.AppendAllText(path, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line + "\r\n", new UTF8Encoding(false));
                }
            }
            catch { }
        }

        // ------------------------------------------------------------------ helpers

        private static Dictionary<string, object> Obj(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key)) return null;
            return dict[key] as Dictionary<string, object>;
        }

        private static Dictionary<string, object> Obj(object value)
        {
            return value as Dictionary<string, object>;
        }

        private static object[] Arr(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key)) return null;
            var value = dict[key] as System.Collections.IEnumerable;
            if (value == null || value is string) return null;
            var list = new List<object>();
            foreach (var item in value) list.Add(item);
            return list.ToArray();
        }

        private static string Str(Dictionary<string, object> dict, string key)
        {
            if (dict == null || !dict.ContainsKey(key)) return null;
            object value = dict[key];
            return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static string FirstNonEmpty(List<Dictionary<string, object>> chain, string key)
        {
            foreach (var node in chain)
            {
                string value = Str(node, key);
                if (!string.IsNullOrEmpty(value)) return value;
            }
            return null;
        }

        private static string ArtifactKey(string name)
        {
            string[] parts = name.Split(':');
            if (parts.Length >= 4) return parts[0] + ":" + parts[1] + ":" + parts[3];
            return parts.Length >= 2 ? parts[0] + ":" + parts[1] : name;
        }

        private static string ArtifactFile(Dictionary<string, object> lib, string name, string librariesRoot)
        {
            var downloads = Obj(lib, "downloads");
            var artifact = downloads != null ? Obj(downloads, "artifact") : null;
            string relative = artifact != null ? Str(artifact, "path") : null;
            if (string.IsNullOrEmpty(relative))
            {
                string[] nameParts = name.Split(':');
                string classifier = nameParts.Length >= 4 ? nameParts[3] : null;
                relative = MavenPath(name, classifier);
            }
            if (string.IsNullOrEmpty(relative)) return null;
            string full = Path.Combine(librariesRoot, relative);
            return File.Exists(full) ? full : null;
        }

        private static string ClassifierFile(Dictionary<string, object> lib, string name, string classifier, string librariesRoot)
        {
            var downloads = Obj(lib, "downloads");
            var classifiers = downloads != null ? Obj(downloads, "classifiers") : null;
            string relative = null;
            if (classifiers != null)
            {
                var node = Obj(classifiers, classifier);
                if (node != null) relative = Str(node, "path");
            }
            if (string.IsNullOrEmpty(relative)) relative = MavenPath(name, classifier);
            string full = Path.Combine(librariesRoot, relative);
            return File.Exists(full) ? full : null;
        }

        private static string MavenPath(string name, string classifier)
        {
            string[] parts = name.Split(':');
            if (parts.Length < 3) return null;
            string group = parts[0].Replace('.', '\\');
            string artifact = parts[1];
            string version = parts[2];
            string file = artifact + "-" + version + (classifier == null ? "" : "-" + classifier) + ".jar";
            return Path.Combine(group, artifact, version, file);
        }

        private static bool RulesOk(Dictionary<string, object> node)
        {
            var rules = Arr(node, "rules");
            if (rules == null) return true;

            bool required = false;
            foreach (var raw in rules)
            {
                var rule = raw as Dictionary<string, object>;
                if (rule == null) continue;

                bool osOk = true;
                var os = Obj(rule, "os");
                if (os != null)
                {
                    string osName = Str(os, "name");
                    if (osName != null && osName != "windows") osOk = false;
                    string arch = Str(os, "arch");
                    if (arch != null && arch == "x86") osOk = false;
                }

                bool featuresOk = true;
                var features = Obj(rule, "features");
                if (features != null) featuresOk = false; // demo/quick-play variants are not enabled here

                bool matches = osOk && featuresOk;
                string action = Str(rule, "action");
                if (action == "allow") { if (matches) required = true; }
                else if (matches) { required = false; }
            }
            return required;
        }

        private static IEnumerable<string> SplitArguments(string text)
        {
            var result = new List<string>();
            var current = new StringBuilder();
            bool quoted = false;
            foreach (char c in text)
            {
                if (c == '"') { quoted = !quoted; current.Append(c); continue; }
                if (!quoted && char.IsWhiteSpace(c))
                {
                    if (current.Length > 0) { result.Add(current.ToString()); current.Length = 0; }
                    continue;
                }
                current.Append(c);
            }
            if (current.Length > 0) result.Add(current.ToString());
            return result;
        }

        private static string Expand(string arg, Dictionary<string, string> tokens)
        {
            string result = arg;
            foreach (var pair in tokens) result = result.Replace(pair.Key, pair.Value);
            if (result.IndexOf(' ') >= 0 && !result.StartsWith("\"")) result = "\"" + result + "\"";
            return result;
        }

        private static void ExtractNatives(string jarPath, string target)
        {
            using (var zip = ZipFile.OpenRead(jarPath))
            {
                foreach (var entry in zip.Entries)
                {
                    if (entry.FullName.EndsWith("/")) continue;
                    if (entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase)) continue;
                    string name = Path.GetFileName(entry.FullName);
                    if (string.IsNullOrEmpty(name)) continue;
                    entry.ExtractToFile(Path.Combine(target, name), true);
                }
            }
        }

        internal static string OfflineUuid(string username)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
                hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
                hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
                var sb = new StringBuilder(32);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }

        private static string OfflineToken(string username)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("PCLOne:" + username + ":" + Guid.NewGuid()));
                var sb = new StringBuilder(32);
                foreach (byte b in hash) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return sb.ToString();
            }
        }
    }
}
