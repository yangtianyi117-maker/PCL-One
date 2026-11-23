const std = @import("std");

const payload = @embedFile("payload.zip");

// 每次发布改这个值；运行时目录里的代码会被重新解压，PCL/ 数据保持不变。
const payload_version = "pclpro-2026-11-22.8";

fn copyDir(src: std.fs.Dir, dst: std.fs.Dir) !void {
    var it = src.iterate();
    while (try it.next()) |entry| {
        switch (entry.kind) {
            .directory => {
                dst.makeDir(entry.name) catch |err| {
                    if (err != error.PathAlreadyExists) return err;
                };
                var s = try src.openDir(entry.name, .{ .iterate = true });
                defer s.close();
                var d = try dst.openDir(entry.name, .{});
                defer d.close();
                try copyDir(s, d);
            },
            .file => {
                var s = try src.openFile(entry.name, .{});
                defer s.close();
                // EasyTier 可能在联机中占用 exe/dll；此时保留旧副本，不影响代码更新。
                var d = dst.createFile(entry.name, .{ .truncate = true }) catch |err| {
                    if (err == error.AccessDenied) continue;
                    return err;
                };
                defer d.close();

                var buffer: [64 * 1024]u8 = undefined;
                while (true) {
                    const read = try s.read(&buffer);
                    if (read == 0) break;
                    try d.writeAll(buffer[0..read]);
                }
            },
            else => {},
        }
    }
}

fn extractPayload(allocator: std.mem.Allocator, exe_dir: []const u8, runtime_dir: []const u8) !void {
    // 暂存目录必须放在 runtime 之外，否则“清理旧代码”会把暂存目录一起删掉。
    const stage_dir = try std.fs.path.join(allocator, &.{ exe_dir, "PCLProRuntime.stage" });
    defer allocator.free(stage_dir);

    std.fs.cwd().deleteTree(stage_dir) catch {};
    try std.fs.cwd().makePath(stage_dir);

    {
        var stage = try std.fs.cwd().openDir(stage_dir, .{});
        defer stage.close();

        var fbs = std.io.fixedBufferStream(@constCast(payload));
        try std.zip.extract(stage, fbs.seekableStream(), .{ .allow_backslashes = true });
    }

    // 清理旧代码，但保留 PCL/ 用户数据。
    {
        var runtime = try std.fs.cwd().openDir(runtime_dir, .{ .iterate = true });
        defer runtime.close();

        var it = runtime.iterate();
        while (try it.next()) |entry| {
            if (std.mem.eql(u8, entry.name, "PCL")) continue;
            if (entry.kind == .directory) {
                runtime.deleteTree(entry.name) catch {};
            } else {
                runtime.deleteFile(entry.name) catch {};
            }
        }

        // payload 会重新放 EasyTier；先删旧副本，避免 zip 解压冲突。
        if (runtime.openDir("PCL", .{})) |pcl| {
            var pcl_dir = pcl;
            pcl_dir.deleteTree("Local/EasyTier") catch {};
            pcl_dir.close();
        } else |_| {}
    }

    {
        var stage = try std.fs.cwd().openDir(stage_dir, .{ .iterate = true });
        defer stage.close();
        var runtime = try std.fs.cwd().openDir(runtime_dir, .{ .iterate = true });
        defer runtime.close();
        try copyDir(stage, runtime);
    }

    std.fs.cwd().deleteTree(stage_dir) catch {};
}

pub fn main() !void {
    var gpa = std.heap.GeneralPurposeAllocator(.{}){};
    defer _ = gpa.deinit();
    const allocator = gpa.allocator();

    const exe_dir = try std.fs.selfExeDirPathAlloc(allocator);
    defer allocator.free(exe_dir);

    const runtime_dir = try std.fs.path.join(allocator, &.{ exe_dir, "PCLProRuntime" });
    defer allocator.free(runtime_dir);

    try std.fs.cwd().makePath(runtime_dir);

    const target_exe = try std.fs.path.join(
        allocator,
        &.{ runtime_dir, "Plain Craft Launcher 2.exe" },
    );
    defer allocator.free(target_exe);

    const marker_path = try std.fs.path.join(
        allocator,
        &.{ runtime_dir, ".payload-version" },
    );
    defer allocator.free(marker_path);

    var needs_extract = true;
    if (std.fs.cwd().openFile(marker_path, .{})) |file| {
        defer file.close();
        if (file.readToEndAlloc(allocator, 128)) |existing| {
            defer allocator.free(existing);
            needs_extract = !std.mem.eql(
                u8,
                std.mem.trim(u8, existing, " \t\r\n"),
                payload_version,
            );
        } else |_| {}
    } else |_| {}

    if (needs_extract) {
        try extractPayload(allocator, exe_dir, runtime_dir);

        var marker = try std.fs.cwd().createFile(marker_path, .{ .truncate = true });
        defer marker.close();
        try marker.writeAll(payload_version);
    }

    // 把 pclpro:// 等命令行参数透传给真正的启动器；否则深链会丢失。
    const args = try std.process.argsAlloc(allocator);
    defer std.process.argsFree(allocator, args);

    var argv = std.ArrayList([]const u8).init(allocator);
    defer argv.deinit();
    try argv.append(target_exe);
    if (args.len > 1) try argv.appendSlice(args[1..]);

    var child = std.process.Child.init(argv.items, allocator);
    child.cwd = runtime_dir;
    try child.spawn();
}
