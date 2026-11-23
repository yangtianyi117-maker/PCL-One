package com.pclpro.mobile;

import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import java.net.DatagramPacket;
import java.net.InetAddress;
import java.net.MulticastSocket;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Probes the local /24 for Minecraft Bedrock servers and listens for Bedrock LAN
 * announcements on 224.0.2.60:4445.
 */
public class ServerScanner {

    private static final String TAG = "PCLScan";

    public interface Listener {
        void onScanStart(int total);

        void onScanFound(ServerInfo server, int done, int total);

        void onScanProgress(int done, int total);

        void onScanFinished(int found);

        void onAnnounce(ServerInfo server);

        void onScanError(String message);
    }

    public static final int ANNOUNCE_PORT = 4445;
    public static final String ANNOUNCE_GROUP = "224.0.2.60";

    private final Handler main = new Handler(Looper.getMainLooper());
    private volatile boolean cancelled;
    private ExecutorService pool;
    private MulticastSocket announceSocket;
    private Thread announceThread;

    public void scan(final int port, final int timeoutMs, final Listener listener) {
        cancel();
        cancelled = false;

        final List<String> hosts = NetUtil.candidateHosts();
        if (hosts.isEmpty()) {
            main.post(new Runnable() {
                @Override
                public void run() {
                    listener.onScanError("没有找到 Wi-Fi 网段，请先连接与电脑相同的 Wi-Fi");
                }
            });
            return;
        }

        final int total = hosts.size();
        Log.i(TAG, "start hosts=" + total + " subnet=" + NetUtil.subnetLabel() + " port=" + port
                + " timeout=" + timeoutMs);
        final AtomicInteger done = new AtomicInteger();
        final AtomicInteger found = new AtomicInteger();
        final AtomicBoolean finished = new AtomicBoolean(false);
        final java.util.Set<String> seenGuids =
                java.util.Collections.synchronizedSet(new java.util.HashSet<String>());

        main.post(new Runnable() {
            @Override
            public void run() {
                listener.onScanStart(total);
            }
        });

        pool = Executors.newFixedThreadPool(48);
        for (final String host : hosts) {
            pool.execute(new Runnable() {
                @Override
                public void run() {
                    if (cancelled) return;
                    final RakNetPing.Result result = RakNetPing.ping(host, port, timeoutMs);
                    if (cancelled) return;

                    if (result != null) {
                        // One physical server may answer on several addresses; keep its first reply.
                        String guid = result.serverGuid == 0 ? "" : Long.toString(result.serverGuid);
                        boolean duplicate = !guid.isEmpty() && !seenGuids.add(guid);
                        if (!duplicate) {
                            final ServerInfo server = new ServerInfo();
                            server.name = result.displayName();
                            server.host = host;
                            server.port = port;
                            server.version = result.version;
                            server.motd = result.motd;
                            server.players = result.players;
                            server.maxPlayers = result.maxPlayers;
                            server.pingMs = result.pingMs;
                            server.lastSeen = System.currentTimeMillis();
                            server.guid = guid;
                            found.incrementAndGet();
                            Log.i(TAG, "found " + host + ":" + port + " name=" + server.name);
                            main.post(new Runnable() {
                                @Override
                                public void run() {
                                    listener.onScanFound(server, done.get(), total);
                                }
                            });
                        }
                    }

                    int current = done.incrementAndGet();
                    if (current % 6 == 0 || current == total) {
                        main.post(new Runnable() {
                            @Override
                            public void run() {
                                listener.onScanProgress(done.get(), total);
                            }
                        });
                    }
                    if (current >= total && finished.compareAndSet(false, true)) {
                        Log.i(TAG, "finished found=" + found.get() + " scanned=" + current);
                        if (pool != null) pool.shutdown();
                        main.post(new Runnable() {
                            @Override
                            public void run() {
                                listener.onScanFinished(found.get());
                            }
                        });
                    }
                }
            });
        }
    }

    public void cancel() {
        cancelled = true;
        if (pool != null) {
            pool.shutdownNow();
            pool = null;
        }
    }

    /** Listens for "[MOTD]name[/MOTD][AD]port[/AD]" broadcasts from Bedrock worlds / Geyser. */
    public void startAnnounce(final Listener listener, final int defaultPort) {
        stopAnnounce();
        try {
            final MulticastSocket socket = new MulticastSocket(ANNOUNCE_PORT);
            socket.setReuseAddress(true);
            socket.joinGroup(InetAddress.getByName(ANNOUNCE_GROUP));
            announceSocket = socket;
            announceThread = new Thread(new Runnable() {
                @Override
                public void run() {
                    byte[] buf = new byte[2048];
                    while (!Thread.currentThread().isInterrupted()) {
                        try {
                            DatagramPacket packet = new DatagramPacket(buf, buf.length);
                            socket.receive(packet);
                            String message = new String(packet.getData(), 0, packet.getLength(),
                                    StandardCharsets.UTF_8);
                            final ServerInfo server = parseAnnounce(message,
                                    packet.getAddress().getHostAddress(), defaultPort);
                            if (server != null) {
                                Log.i(TAG, "announce " + server.address() + " name=" + server.name);
                                main.post(new Runnable() {
                                    @Override
                                    public void run() {
                                        listener.onAnnounce(server);
                                    }
                                });
                            }
                        } catch (Exception e) {
                            break;
                        }
                    }
                }
            });
            announceThread.setDaemon(true);
            announceThread.setName("pclpro-announce");
            announceThread.start();
        } catch (Exception ignored) {
        }
    }

    public void stopAnnounce() {
        try {
            if (announceSocket != null) {
                announceSocket.close();
                announceSocket = null;
            }
        } catch (Exception ignored) {
        }
        if (announceThread != null) {
            announceThread.interrupt();
            announceThread = null;
        }
    }

    static ServerInfo parseAnnounce(String message, String host, int defaultPort) {
        if (message == null) return null;
        int motdStart = message.indexOf("[MOTD]");
        int motdEnd = message.indexOf("[/MOTD]");
        if (motdStart < 0 || motdEnd <= motdStart) return null;

        String name = RakNetPing.strip(message.substring(motdStart + 6, motdEnd));
        if (name.isEmpty()) return null;

        int port = defaultPort;
        int adStart = message.indexOf("[AD]");
        int adEnd = message.indexOf("[/AD]");
        if (adStart >= 0 && adEnd > adStart) {
            try {
                port = Integer.parseInt(message.substring(adStart + 4, adEnd).trim());
            } catch (Exception ignored) {
            }
        }

        ServerInfo s = new ServerInfo();
        s.name = name;
        s.host = host;
        s.port = port;
        s.pingMs = 0;
        s.lastSeen = System.currentTimeMillis();
        s.motd = name;
        return s;
    }
}
