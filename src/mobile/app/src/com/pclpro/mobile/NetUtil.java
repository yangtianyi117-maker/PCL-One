package com.pclpro.mobile;

import java.net.Inet4Address;
import java.net.InetAddress;
import java.net.InterfaceAddress;
import java.net.NetworkInterface;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

/** Local network helpers: find the phone's IPv4 addresses and the /24 neighbourhood to probe. */
public final class NetUtil {

    private NetUtil() {
    }

    public static List<String> localIpv4() {
        List<String> out = new ArrayList<>();
        try {
            for (NetworkInterface ni : java.util.Collections.list(NetworkInterface.getNetworkInterfaces())) {
                if (!ni.isUp() || ni.isLoopback()) continue;
                for (InterfaceAddress ia : ni.getInterfaceAddresses()) {
                    InetAddress addr = ia.getAddress();
                    if (!(addr instanceof Inet4Address)) continue;
                    String ip = addr.getHostAddress();
                    if (ip == null) continue;
                    if (ip.startsWith("127.") || ip.startsWith("169.254.") || ip.startsWith("0.")) continue;
                    if (!out.contains(ip)) out.add(ip);
                }
            }
        } catch (Exception ignored) {
        }
        return out;
    }

    public static String displayAddress() {
        List<String> ips = localIpv4();
        return ips.isEmpty() ? "未连接 Wi-Fi" : ips.get(0);
    }

    /**
     * Builds the probe list: every host in the /24 of each local IPv4 address, excluding our own.
     */
    public static List<String> candidateHosts() {
        Set<String> hosts = new LinkedHashSet<>();
        Set<String> own = new HashSet<>(localIpv4());
        Set<String> prefixes = new LinkedHashSet<>();

        for (String ip : own) {
            int dot = ip.lastIndexOf('.');
            if (dot > 0) prefixes.add(ip.substring(0, dot + 1));
        }
        for (String prefix : prefixes) {
            for (int i = 1; i <= 254; i++) {
                String host = prefix + i;
                if (!own.contains(host)) hosts.add(host);
            }
        }
        return new ArrayList<>(hosts);
    }

    public static String subnetLabel() {
        List<String> ips = localIpv4();
        if (ips.isEmpty()) return "未连接 Wi-Fi";
        String ip = ips.get(0);
        int dot = ip.lastIndexOf('.');
        return dot > 0 ? ip.substring(0, dot + 1) + "0/24" : ip;
    }
}
