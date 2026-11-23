package com.pclpro.mobile;

import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.nio.ByteBuffer;
import java.nio.ByteOrder;
import java.nio.charset.StandardCharsets;

/**
 * Minecraft Bedrock (RakNet) unconnected ping.
 * <p>
 * Pure java.net / java.nio code with no Android dependency, so the same class is used by the
 * desktop protocol test harness (tools/PingTest.java).
 * <p>
 * Request (0x01): id, time(8), offline magic(16), client guid(8) = 33 bytes.
 * Reply (0x1C): id, time(8), server guid(8), offline magic(16), string length(2), UTF-8
 * "MOTD;protocol;version;players;maxPlayers;serverId;levelName;gameMode;gameModeNum;port4;port6".
 */
public final class RakNetPing {

    public static final int DEFAULT_PORT = 19132;

    private static final byte[] MAGIC = {
            0x00, (byte) 0xFF, (byte) 0xFF, 0x00, (byte) 0xFE, (byte) 0xFE, (byte) 0xFE, (byte) 0xFE,
            (byte) 0xFD, (byte) 0xFD, (byte) 0xFD, (byte) 0xFD, 0x12, 0x34, 0x56, 0x78
    };

    private RakNetPing() {
    }

    public static final class Result {
        public String motd = "";
        public String level = "";
        public String version = "";
        public String name = "";
        public int players = -1;
        public int maxPlayers = -1;
        public int gameMode = -1;
        public long serverGuid = 0;
        public long pingMs = -1;

        public String displayName() {
            if (name != null && !name.isEmpty()) return name;
            if (level != null && !level.isEmpty()) return level;
            return "Minecraft 服务器";
        }
    }

    public static Result ping(String host, int port, int timeoutMs) {
        DatagramSocket socket = null;
        try {
            InetAddress address = InetAddress.getByName(host);
            byte[] request = buildRequest();
            socket = new DatagramSocket();
            int t = Math.max(120, timeoutMs);
            socket.setSoTimeout(t);
            long start = System.nanoTime();
            socket.send(new DatagramPacket(request, request.length, address, port));

            byte[] buf = new byte[2048];
            DatagramPacket in = new DatagramPacket(buf, buf.length);
            long deadline = System.currentTimeMillis() + t;
            while (System.currentTimeMillis() < deadline) {
                socket.receive(in);
                int len = in.getLength();
                byte[] data = in.getData();
                if (len < 35 || (data[0] & 0xFF) != 0x1C) continue;
                Result r = parse(data, len);
                if (r == null) continue;
                r.pingMs = Math.max(1L, (System.nanoTime() - start) / 1_000_000L);
                return r;
            }
            return null;
        } catch (Exception e) {
            return null;
        } finally {
            if (socket != null) {
                try {
                    socket.close();
                } catch (Exception ignored) {
                }
            }
        }
    }

    static byte[] buildRequest() {
        ByteBuffer b = ByteBuffer.allocate(33).order(ByteOrder.BIG_ENDIAN);
        b.put((byte) 0x01);
        b.putLong(System.currentTimeMillis());
        b.put(MAGIC);
        b.putLong(0x50434C50726F4D4CL); // "PCLProML" style client guid
        return b.array();
    }

    static Result parse(byte[] buf, int len) {
        try {
            ByteBuffer b = ByteBuffer.wrap(buf, 0, len).order(ByteOrder.BIG_ENDIAN);
            b.get();              // 0x1C
            b.getLong();          // echoed time
            long serverGuid = b.getLong();
            for (int i = 0; i < 16; i++) b.get(); // offline magic
            int strLen = b.getShort() & 0xFFFF;
            if (strLen <= 0 || b.remaining() < strLen) return null;
            byte[] raw = new byte[strLen];
            b.get(raw);
            String payload = new String(raw, StandardCharsets.UTF_8);
            String[] f = payload.split(";", -1);

            Result r = new Result();
            r.serverGuid = serverGuid;
            // Modern Bedrock servers reply "MCPE;MOTD;protocol;version;players;max;serverId;level;..."
            // Older/other implementations omit the leading edition field.
            int off = (f.length > 1 && "MCPE".equalsIgnoreCase(f[0].trim())) ? 1 : 0;
            r.motd = strip(f.length > off ? f[off] : "");
            r.version = f.length > off + 2 ? strip(f[off + 2]) : "";
            r.players = f.length > off + 3 ? parseInt(f[off + 3]) : -1;
            r.maxPlayers = f.length > off + 4 ? parseInt(f[off + 4]) : -1;
            r.level = f.length > off + 6 ? strip(f[off + 6]) : "";
            r.gameMode = f.length > off + 8 ? parseInt(f[off + 8]) : -1;
            r.name = !r.level.isEmpty() ? r.level : r.motd;
            if (r.name.isEmpty()) r.name = "Minecraft 服务器";
            if (r.name.length() > 48) r.name = r.name.substring(0, 48);
            return r;
        } catch (Exception e) {
            return null;
        }
    }

    static int parseInt(String s) {
        try {
            return Integer.parseInt(s.trim());
        } catch (Exception e) {
            return -1;
        }
    }

    /** Removes Minecraft colour / formatting codes such as §a, §l. */
    public static String strip(String s) {
        if (s == null) return "";
        return s.replaceAll("\u00A7[0-9a-fk-orA-FK-OR]", "").trim();
    }
}
