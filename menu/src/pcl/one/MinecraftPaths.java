package pcl.one;

import java.io.File;

import net.minecraft.client.Minecraft;

/** Locates the PCL One package root from the running game directory. */
final class MinecraftPaths
{
    private MinecraftPaths()
    {
    }

    /**
     * Walks up from the game directory until a folder containing PCL One.exe
     * (package layout) or pcl-one.json (source layout) is found.
     */
    static File packageRoot()
    {
        try
        {
            File dir = Minecraft.getInstance().gameDirectory;
            for (int i = 0; i < 8 && dir != null; i++, dir = dir.getParentFile())
            {
                if (new File(dir, "PCL One.exe").isFile()) return dir;
                if (new File(dir, "pcl-one.json").isFile()) return dir;
            }
        }
        catch (Throwable ignored)
        {
        }
        return null;
    }
}
