package pcl.one;

import java.io.File;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.time.LocalDateTime;

/** Writes menu failures next to the package so a crash is diagnosable without a UI. */
final class Diag
{
    private Diag()
    {
    }

    static void log(String where, Throwable t)
    {
        try
        {
            File root = MinecraftPaths.packageRoot();
            File out = root == null ? new File("PCLOne-menu.log") : new File(root, "PCLOne-menu.log");
            try (PrintWriter writer = new PrintWriter(new FileWriter(out, true)))
            {
                writer.println(LocalDateTime.now() + "  " + where);
                t.printStackTrace(writer);
                writer.println();
            }
        }
        catch (Throwable ignored)
        {
        }
    }

    static void log(String message)
    {
        try
        {
            File root = MinecraftPaths.packageRoot();
            File out = root == null ? new File("PCLOne-menu.log") : new File(root, "PCLOne-menu.log");
            try (PrintWriter writer = new PrintWriter(new FileWriter(out, true)))
            {
                writer.println(LocalDateTime.now() + "  " + message);
            }
        }
        catch (Throwable ignored)
        {
        }
    }
}
