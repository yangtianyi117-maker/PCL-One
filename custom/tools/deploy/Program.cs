using System.Security.Cryptography;
using Renci.SshNet;

var host = Environment.GetEnvironmentVariable("PCL_VPS_HOST") ?? "124.221.231.213";
var user = Environment.GetEnvironmentVariable("PCL_VPS_USER") ?? "root";
var password = Environment.GetEnvironmentVariable("PCL_VPS_PASSWORD")
               ?? throw new InvalidOperationException("PCL_VPS_PASSWORD is not set");
var port = int.TryParse(Environment.GetEnvironmentVariable("PCL_VPS_PORT"), out var p) ? p : 22;

if (args.Length == 0)
{
    Console.Error.WriteLine("usage: sshdeploy probe|exec <command>|upload <local> <remote>|mkdir <remote>");
    return 2;
}

try
{
    using var ssh = new SshClient(host, port, user, password);
    ssh.Connect();
    Console.WriteLine($"CONNECTED {user}@{host}:{port}");

    switch (args[0])
    {
        case "probe":
        {
            var cmd = ssh.RunCommand("id; hostname; uname -a; command -v python3 || true; command -v systemctl || true");
            Console.WriteLine(cmd.Result);
            Console.Error.WriteLine(cmd.Error);
            return cmd.ExitStatus ?? -1;
        }
        case "exec":
        {
            var command = string.Join(' ', args.Skip(1));
            var cmd = ssh.RunCommand(command);
            Console.WriteLine(cmd.Result);
            Console.Error.WriteLine(cmd.Error);
            return cmd.ExitStatus ?? -1;
        }
        case "mkdir":
        {
            var cmd = ssh.RunCommand($"mkdir -p '{args[1]}'");
            Console.WriteLine(cmd.Result);
            Console.Error.WriteLine(cmd.Error);
            return cmd.ExitStatus ?? -1;
        }
        case "upload":
        {
            var localPath = args[1];
            var remotePath = args[2];
            var localLength = new FileInfo(localPath).Length;
            string localHash;
            using (var hashStream = File.OpenRead(localPath))
            using (var sha = SHA256.Create())
                localHash = Convert.ToHexString(await sha.ComputeHashAsync(hashStream)).ToLowerInvariant();

            Exception? lastError = null;
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                try
                {
                    using (var sftp = new SftpClient(host, port, user, password))
                    {
                        sftp.Connect();
                        sftp.BufferSize = 256 * 1024;
                        await using var stream = File.OpenRead(localPath);
                        sftp.UploadFile(stream, remotePath, true);
                    }

                    using var verifySsh = new SshClient(host, port, user, password);
                    verifySsh.Connect();
                    var check = verifySsh.RunCommand($"sha256sum '{remotePath}'");
                    var remoteHash = check.Result.Trim().Split(' ')[0].ToLowerInvariant();
                    if (check.ExitStatus == 0 && remoteHash == localHash)
                    {
                        Console.WriteLine($"UPLOADED {localPath} -> {remotePath} ({localLength} bytes, sha256 {localHash})");
                        return 0;
                    }

                    lastError = new IOException($"remote hash mismatch: {remoteHash} != {localHash}");
                }
                catch (Exception ex)
                {
                    lastError = ex;
                }

                Console.Error.WriteLine($"upload attempt {attempt}/3 failed: {lastError?.Message}");
                if (attempt < 3) await Task.Delay(1500);
            }

            Console.Error.WriteLine($"UPLOAD FAILED {localPath} -> {remotePath}: {lastError?.Message}");
            return 1;
        }
        default:
            Console.Error.WriteLine("unknown command");
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR {ex.GetType().Name}: {ex.Message}");
    return 1;
}
