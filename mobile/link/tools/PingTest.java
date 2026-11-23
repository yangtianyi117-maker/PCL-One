import com.pclpro.mobile.RakNetPing;

/**
 * Desktop harness for the Android app's RakNet ping implementation.
 * Usage: java PingTest <host> [port] [timeoutMs]
 */
public class PingTest {
    public static void main(String[] args) throws Exception {
        String host = args.length > 0 ? args[0] : "127.0.0.1";
        int port = args.length > 1 ? Integer.parseInt(args[1]) : 19132;
        int timeout = args.length > 2 ? Integer.parseInt(args[2]) : 800;

        long start = System.currentTimeMillis();
        RakNetPing.Result r = RakNetPing.ping(host, port, timeout);
        long took = System.currentTimeMillis() - start;

        if (r == null) {
            System.out.println("NULL host=" + host + " port=" + port + " took=" + took + "ms");
            return;
        }
        System.out.println("OK host=" + host + " port=" + port + " took=" + took + "ms");
        System.out.println("  name    = " + r.displayName());
        System.out.println("  motd    = " + r.motd);
        System.out.println("  version = " + r.version);
        System.out.println("  players = " + r.players + "/" + r.maxPlayers);
        System.out.println("  ping    = " + r.pingMs + "ms");
    }
}
