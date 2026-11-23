package com.pclpro.mobile;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

/**
 * Minimal Minecraft Java Edition Server List Ping (1.7+ status protocol).
 *
 * <p>Only used to show the PC-side status of the official server. The phone plays Bedrock, so a
 * failure here never blocks "进入": the Bedrock address is still valid.</p>
 */
public final class JavaPing {

    public static final class Result {
        public String version = "";
        public String motd = "";
        public int players = -1;
        public int maxPlayers = -1;
        public long pingMs = -1;
    }

    private JavaPing() {
    }

    public static Result ping(String host, int port, int timeoutMs) {
        Socket socket = null;
        try {
            int timeout = Math.max(300, timeoutMs);
            socket = new Socket();
            socket.connect(new InetSocketAddress(host, port), timeout);
            socket.setSoTimeout(timeout);
            long start = System.nanoTime();

            ByteArrayOutputStream handshake = new ByteArrayOutputStream();
            writeVarInt(handshake, 0x00);                 // packet id: handshake
            writeVarInt(handshake, 777);                  // protocol (1.21.9+/26.x); status ignores it
            writeString(handshake, host);
            handshake.write((port >> 8) & 0xFF);
            handshake.write(port & 0xFF);
            writeVarInt(handshake, 1);                    // next state: status

            OutputStream out = socket.getOutputStream();
            writeVarInt(out, handshake.size());
            handshake.writeTo(out);

            ByteArrayOutputStream request = new ByteArrayOutputStream();
            writeVarInt(request, 0x00);
            writeVarInt(out, request.size());
            request.writeTo(out);
            out.flush();

            InputStream in = socket.getInputStream();
            int length = readVarInt(in);
            if (length <= 0) return null;
            byte[] packet = new byte[length];
            int read = 0;
            while (read < length) {
                int count = in.read(packet, read, length - read);
                if (count <= 0) return null;
                read += count;
            }

            int[] cursor = {0};
            int packetId = readVarInt(packet, cursor);
            if (packetId != 0x00) return null;
            String json = readString(packet, cursor);
            JSONObject root = new JSONObject(json);

            Result result = new Result();
            JSONObject version = root.optJSONObject("version");
            if (version != null) result.version = version.optString("name", "");

            JSONObject players = root.optJSONObject("players");
            if (players != null) {
                result.players = players.optInt("online", -1);
                result.maxPlayers = players.optInt("max", -1);
            }

            result.motd = plainText(root.opt("description"));
            result.pingMs = Math.max(1L, (System.nanoTime() - start) / 1_000_000L);
            return result;
        } catch (Exception ignored) {
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

    private static String plainText(Object description) {
        if (description == null) return "";
        if (description instanceof String) return RakNetPing.strip((String) description);
        if (description instanceof JSONObject) {
            JSONObject object = (JSONObject) description;
            StringBuilder sb = new StringBuilder();
            String text = object.optString("text", "");
            if (!text.isEmpty()) sb.append(text);
            org.json.JSONArray extra = object.optJSONArray("extra");
            if (extra != null) {
                for (int i = 0; i < extra.length(); i++) {
                    Object child = extra.opt(i);
                    if (child instanceof JSONObject) {
                        sb.append(((JSONObject) child).optString("text", ""));
                    } else if (child != null) {
                        sb.append(child);
                    }
                }
            }
            return RakNetPing.strip(sb.toString());
        }
        return "";
    }

    private static void writeString(ByteArrayOutputStream out, String value) throws java.io.IOException {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        writeVarInt(out, bytes.length);
        out.write(bytes, 0, bytes.length);
    }

    private static void writeVarInt(java.io.OutputStream out, int value) throws java.io.IOException {
        while (true) {
            int bits = value & 0x7F;
            value >>>= 7;
            if (value != 0) {
                out.write(bits | 0x80);
            } else {
                out.write(bits);
                return;
            }
        }
    }

    private static int readVarInt(InputStream in) throws Exception {
        int value = 0;
        int position = 0;
        while (true) {
            int current = in.read();
            if (current < 0) throw new Exception("eof");
            value |= (current & 0x7F) << position;
            if ((current & 0x80) == 0) return value;
            position += 7;
            if (position >= 35) throw new Exception("varint too big");
        }
    }

    private static int readVarInt(byte[] data, int[] cursor) throws Exception {
        int value = 0;
        int position = 0;
        while (true) {
            if (cursor[0] >= data.length) throw new Exception("eof");
            int current = data[cursor[0]++] & 0xFF;
            value |= (current & 0x7F) << position;
            if ((current & 0x80) == 0) return value;
            position += 7;
            if (position >= 35) throw new Exception("varint too big");
        }
    }

    private static String readString(byte[] data, int[] cursor) throws Exception {
        int length = readVarInt(data, cursor);
        if (length < 0 || cursor[0] + length > data.length) throw new Exception("bad string");
        String value = new String(data, cursor[0], length, StandardCharsets.UTF_8);
        cursor[0] += length;
        return value;
    }
}
