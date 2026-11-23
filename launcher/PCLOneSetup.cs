using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace PCLOneSfx
{
    /// <summary>
    /// Silent self-extractor. No window, no dialog, no third-party UI:
    /// - double-click: extract next to the exe (or %LOCALAPPDATA%\Programs) and start the game;
    /// - CLI: --verify / --verify-full / --extract &lt;dir&gt; / --version, result written to a log.
    /// </summary>
    internal sealed class Payload
    {
        public const string Magic = "PCL1SFX1";
        private const int FooterSize = 24;

        public string ExePath;
        public long Offset;
        public long Length;

        public static Payload Open(string exePath)
        {
            using (var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (fs.Length < FooterSize + 4) throw new InvalidDataException("文件太小，不是 PCL One 离线包。");
                fs.Seek(-FooterSize, SeekOrigin.End);
                var footer = new byte[FooterSize];
                ReadFull(fs, footer, 0, FooterSize);

                string magic = Encoding.ASCII.GetString(footer, 0, 8);
                if (magic != Magic) throw new InvalidDataException("尾部标记不匹配，文件可能没有复制完整。");

                long offset = BitConverter.ToInt64(footer, 8);
                long length = BitConverter.ToInt64(footer, 16);
                if (offset < 0 || length <= 0 || offset + length > fs.Length - FooterSize)
                    throw new InvalidDataException("离线包数据长度无效，文件可能损坏。");

                return new Payload { ExePath = exePath, Offset = offset, Length = length };
            }
        }

        public Stream OpenRead()
        {
            var fs = new FileStream(ExePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new SubStream(fs, Offset, Length);
        }

        internal static void ReadFull(Stream stream, byte[] buffer, int offset, int count)
        {
            int done = 0;
            while (done < count)
            {
                int n = stream.Read(buffer, offset + done, count - done);
                if (n <= 0) throw new EndOfStreamException();
                done += n;
            }
        }
    }

    internal sealed class SubStream : Stream
    {
        private readonly Stream _base;
        private readonly long _start;
        private readonly long _length;
        private long _pos;

        public SubStream(Stream baseStream, long start, long length) { _base = baseStream; _start = start; _length = length; }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return _length; } }
        public override long Position
        {
            get { return _pos; }
            set { if (value < 0) throw new ArgumentOutOfRangeException("value"); _pos = value; }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_pos >= _length) return 0;
            long remain = _length - _pos;
            if (count > remain) count = (int)remain;
            _base.Position = _start + _pos;
            int n = _base.Read(buffer, offset, count);
            _pos += n;
            return n;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long target;
            switch (origin)
            {
                case SeekOrigin.Begin: target = offset; break;
                case SeekOrigin.Current: target = _pos + offset; break;
                case SeekOrigin.End: target = _length + offset; break;
                default: throw new ArgumentException("origin");
            }
            if (target < 0) throw new IOException("seek before start");
            _pos = target;
            return _pos;
        }

        public override void Flush() { }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _base.Dispose();
            base.Dispose(disposing);
        }
    }

    internal static class Extractor
    {
        public static string RootFolder { get { return SfxInfo.RootFolder; } }

        public static void Extract(Payload payload, string targetParent, Action<string> log)
        {
            Directory.CreateDirectory(targetParent);
            try
            {
                ExtractFromStream(payload.OpenRead(), targetParent);
            }
            catch (InvalidDataException ex)
            {
                if (log != null) log("substream failed, retrying via temp copy: " + ex.Message);
                string temp = Path.Combine(Path.GetTempPath(), "pcl-one-payload-" + Guid.NewGuid().ToString("N") + ".zip");
                try
                {
                    using (Stream input = payload.OpenRead())
                    using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                        CopyStream(input, output);
                    using (var fs = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read))
                        ExtractFromStream(fs, targetParent);
                }
                finally
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
            }
        }

        private static void ExtractFromStream(Stream stream, string targetParent)
        {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                string root = Path.Combine(targetParent, RootFolder);
                Directory.CreateDirectory(root);

                foreach (var entry in zip.Entries)
                {
                    string relative = entry.FullName.Replace('/', '\\');
                    string dest = Path.Combine(targetParent, relative);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(dest);
                        continue;
                    }

                    string dir = Path.GetDirectoryName(dest);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                    using (Stream input = entry.Open())
                    using (var output = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                        CopyStream(input, output);
                }
            }
        }

        private static void CopyStream(Stream input, Stream output)
        {
            var buffer = new byte[1024 * 1024];
            int n;
            while ((n = input.Read(buffer, 0, buffer.Length)) > 0) output.Write(buffer, 0, n);
        }

        public static string Verify(Payload payload, bool full)
        {
            var sb = new StringBuilder();
            using (Stream stream = payload.OpenRead())
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                int entries = zip.Entries.Count;
                if (entries < 100) throw new InvalidDataException("离线包内容异常（条目太少：" + entries + "）。");

                bool hasShell = false;
                bool hasConfig = false;
                long unpacked = 0;
                foreach (var entry in zip.Entries)
                {
                    unpacked += entry.Length;
                    string name = entry.FullName.Replace('\\', '/');
                    if (name == SfxInfo.RootFolder + "/PCL One.exe") hasShell = true;
                    if (name == SfxInfo.RootFolder + "/pcl-one.json") hasConfig = true;
                    if (full)
                    {
                        using (Stream s = entry.Open())
                        {
                            var buf = new byte[65536];
                            while (s.Read(buf, 0, buf.Length) > 0) { }
                        }
                    }
                }

                if (!hasShell) throw new InvalidDataException("离线包里缺少 PCL One.exe。");
                if (!hasConfig) throw new InvalidDataException("离线包里缺少 pcl-one.json。");

                sb.AppendLine("verify OK");
                sb.AppendLine("version   : " + SfxInfo.Version);
                sb.AppendLine("entries   : " + entries);
                sb.AppendLine("payload   : " + (payload.Length / 1024 / 1024) + " MB compressed");
                sb.AppendLine("unpacked  : " + (unpacked / 1024 / 1024) + " MB");
                sb.AppendLine("crc       : " + (full ? "checked" : "not checked"));
            }
            return sb.ToString();
        }
    }

    internal static class Program
    {
        private static string _logPath;

        private static int Main(string[] args)
        {
            string exe = typeof(Program).Assembly.Location;
            try
            {
                var payload = Payload.Open(exe);
                _logPath = LogPath(exe);

                if (args.Length > 0)
                {
                    string command = args[0].ToLowerInvariant();
                    if (command == "--version")
                    {
                        Log("PCL One 离线包 " + SfxInfo.Version);
                        return 0;
                    }
                    if (command == "--verify" || command == "--verify-full")
                    {
                        string report = Extractor.Verify(payload, command == "--verify-full");
                        Log(report.TrimEnd());
                        return 0;
                    }
                    if (command == "--extract")
                    {
                        string target = args.Length > 1 ? args[1] : ChooseParent(exe);
                        Extractor.Extract(payload, target, Log);
                        Log("OK: " + Path.Combine(target, Extractor.RootFolder));
                        return 0;
                    }
                    Log("unknown argument: " + command);
                    return 2;
                }

                string parent = ChooseParent(exe);
                string appDir = Path.Combine(parent, Extractor.RootFolder);
                string shell = Path.Combine(appDir, "PCL One.exe");
                string versionFile = Path.Combine(appDir, "PCLOne-version.txt");

                bool installed = File.Exists(shell) && File.Exists(versionFile) &&
                                 File.ReadAllText(versionFile).Trim() == SfxInfo.Version;

                if (!installed)
                {
                    Log("installing to " + appDir);
                    Extractor.Extract(payload, parent, Log);
                    File.WriteAllText(versionFile, SfxInfo.Version, new UTF8Encoding(false));
                    Log("installed");
                }
                else
                {
                    Log("already installed, launching");
                }

                if (!File.Exists(shell)) throw new FileNotFoundException("缺少 PCL One.exe", shell);
                var start = new System.Diagnostics.ProcessStartInfo(shell, "");
                start.WorkingDirectory = appDir;
                start.UseShellExecute = true;
                System.Diagnostics.Process.Start(start);
                Log("launched " + shell);
                return 0;
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex);
                return 1;
            }
        }

        private static string ChooseParent(string exe)
        {
            string dir = Path.GetDirectoryName(exe);
            try
            {
                string probe = Path.Combine(dir, ".pclone-write-test");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return dir;
            }
            catch
            {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(local, "Programs");
            }
        }

        private static string LogPath(string exe)
        {
            try
            {
                string dir = Path.GetDirectoryName(exe);
                string probe = Path.Combine(dir, ".pclone-log-test");
                File.WriteAllText(probe, "x");
                File.Delete(probe);
                return Path.Combine(dir, "PCLOne-sfx.log");
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "PCLOne-sfx.log");
            }
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(_logPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + "\r\n",
                    new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
