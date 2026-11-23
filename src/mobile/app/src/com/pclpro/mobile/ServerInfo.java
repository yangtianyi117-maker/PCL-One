package com.pclpro.mobile;

import org.json.JSONException;
import org.json.JSONObject;

/** One discovered or manually added Minecraft (Bedrock) server / PCL Pro room. */
public class ServerInfo {

    public String name = "";
    public String host = "";
    public int port = 19132;
    public String version = "";
    public String motd = "";
    public int players = -1;
    public int maxPlayers = -1;
    public long pingMs = -1;
    public String guid = "";
    public long lastSeen = 0;
    public boolean manual = false;

    public String address() {
        return host + ":" + port;
    }

    public String playersText() {
        if (players < 0 || maxPlayers < 0) return "人数未知";
        return players + " / " + maxPlayers + " 人在线";
    }

    public String subtitle() {
        StringBuilder sb = new StringBuilder();
        if (!version.isEmpty()) sb.append("Bedrock ").append(version);
        if (players >= 0) {
            if (sb.length() > 0) sb.append(" · ");
            sb.append(players).append('/').append(maxPlayers).append(" 人");
        }
        if (sb.length() == 0) sb.append("Minecraft 服务器");
        return sb.toString();
    }

    public int pingColor() {
        if (pingMs < 0) return Ui.SUB;
        if (pingMs < 90) return Ui.GREEN;
        if (pingMs < 220) return Ui.ORANGE;
        return Ui.RED;
    }

    public String pingText() {
        return pingMs < 0 ? "--" : pingMs + " ms";
    }

    public JSONObject toJson() {
        JSONObject o = new JSONObject();
        try {
            o.put("name", name);
            o.put("host", host);
            o.put("port", port);
            o.put("version", version);
            o.put("motd", motd);
            o.put("players", players);
            o.put("maxPlayers", maxPlayers);
            o.put("pingMs", pingMs);
            o.put("guid", guid);
            o.put("lastSeen", lastSeen);
            o.put("manual", manual);
        } catch (JSONException ignored) {
        }
        return o;
    }

    public static ServerInfo fromJson(JSONObject o) {
        ServerInfo s = new ServerInfo();
        s.name = o.optString("name", "");
        s.host = o.optString("host", "");
        s.port = o.optInt("port", 19132);
        s.version = o.optString("version", "");
        s.motd = o.optString("motd", "");
        s.players = o.optInt("players", -1);
        s.maxPlayers = o.optInt("maxPlayers", -1);
        s.pingMs = o.optLong("pingMs", -1);
        s.guid = o.optString("guid", "");
        s.lastSeen = o.optLong("lastSeen", 0);
        s.manual = o.optBoolean("manual", false);
        return s;
    }

    public static ServerInfo copy(ServerInfo other) {
        ServerInfo s = new ServerInfo();
        s.name = other.name;
        s.host = other.host;
        s.port = other.port;
        s.version = other.version;
        s.motd = other.motd;
        s.players = other.players;
        s.maxPlayers = other.maxPlayers;
        s.pingMs = other.pingMs;
        s.guid = other.guid;
        s.lastSeen = other.lastSeen;
        s.manual = other.manual;
        return s;
    }
}
