package com.pclpro.mobile;

import android.content.Context;
import android.content.SharedPreferences;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/** Persistent settings and saved servers. */
public class Prefs {

    private static final String FILE = "pclpro_mobile";
    private final SharedPreferences sp;

    public Prefs(Context c) {
        sp = c.getSharedPreferences(FILE, Context.MODE_PRIVATE);
    }

    public int port() {
        return sp.getInt("port", 19132);
    }

    public void setPort(int p) {
        sp.edit().putInt("port", p).apply();
    }

    public int timeoutMs() {
        return sp.getInt("timeout", 450);
    }

    public void setTimeout(int ms) {
        sp.edit().putInt("timeout", ms).apply();
    }

    public String roomCode() {
        return sp.getString("room", "");
    }

    public void setRoomCode(String code) {
        sp.edit().putString("room", code == null ? "" : code.trim()).apply();
    }

    public String serverAddr() {
        return sp.getString("server", "");
    }

    public void setServerAddr(String addr) {
        sp.edit().putString("server", addr == null ? "" : addr.trim()).apply();
    }

    public String officialJava() {
        return sp.getString("officialJava", OfficialServers.JAVA);
    }

    public void setOfficialJava(String addr) {
        sp.edit().putString("officialJava", addr == null ? "" : addr.trim()).apply();
    }

    public String officialBedrock() {
        return sp.getString("officialBedrock", OfficialServers.BEDROCK);
    }

    public void setOfficialBedrock(String addr) {
        sp.edit().putString("officialBedrock", addr == null ? "" : addr.trim()).apply();
    }

    public String roomNetwork() {
        return sp.getString("roomNetwork", "");
    }

    public void setRoomNetwork(String value) {
        sp.edit().putString("roomNetwork", value == null ? "" : value.trim()).apply();
    }

    public String roomSecret() {
        return sp.getString("roomSecret", "");
    }

    public void setRoomSecret(String value) {
        sp.edit().putString("roomSecret", value == null ? "" : value.trim()).apply();
    }

    public String roomHost() {
        return sp.getString("roomHost", "10.114.51.41");
    }

    public void setRoomHost(String value) {
        sp.edit().putString("roomHost", value == null ? "" : value.trim()).apply();
    }

    public int roomPort() {
        return sp.getInt("roomPort", 19132);
    }

    public void setRoomPort(int port) {
        sp.edit().putInt("roomPort", port).apply();
    }

    public void saveServers(List<ServerInfo> servers) {
        JSONArray arr = new JSONArray();
        for (ServerInfo s : servers) {
            if (s.manual) arr.put(s.toJson());
        }
        sp.edit().putString("servers", arr.toString()).apply();
    }

    public List<ServerInfo> loadServers() {
        List<ServerInfo> out = new ArrayList<>();
        try {
            JSONArray arr = new JSONArray(sp.getString("servers", "[]"));
            for (int i = 0; i < arr.length(); i++) {
                JSONObject o = arr.optJSONObject(i);
                if (o != null) {
                    ServerInfo s = ServerInfo.fromJson(o);
                    s.manual = true;
                    if (!s.host.isEmpty()) out.add(s);
                }
            }
        } catch (Exception ignored) {
        }
        return out;
    }
}
