package pcl.one;

import java.io.File;
import java.io.FileWriter;
import java.io.PrintWriter;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphicsExtractor;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.components.EditBox;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

/** Username / memory editor for PCLOne-settings.json (the launcher reads the same file). */
public final class SettingsScreen extends Screen
{
    private static final Pattern USER = Pattern.compile("\"username\"\\s*:\\s*\"([^\"]*)\"");
    private static final Pattern MEM = Pattern.compile("\"memoryMb\"\\s*:\\s*(\\d+)");

    private EditBox user;
    private EditBox memory;
    private String status;

    public SettingsScreen()
    {
        super(Minecraft.getInstance(), Minecraft.getInstance().font, Component.literal("PCL One · 设置"));
    }

    @Override
    protected void init()
    {
        int cx = this.width / 2;

        user = new EditBox(this.font, cx - 100, 60, 200, 20, Component.literal("离线用户名"));
        user.setMaxLength(16);
        user.setValue(read("username", "Steve"));
        addRenderableWidget(user);

        memory = new EditBox(this.font, cx - 100, 106, 200, 20, Component.literal("内存 MB"));
        memory.setMaxLength(5);
        memory.setValue(read("memoryMb", "4096"));
        addRenderableWidget(memory);

        add(cx - 100, 146, 96, 20, "保存", this::save);
        add(cx + 4, 146, 96, 20, "返回", () -> Minecraft.getInstance().setScreenAndShow(new PclOneScreen(0)));
    }

    private void add(int x, int y, int w, int h, String label, Runnable action)
    {
        addRenderableWidget(Button.builder(Component.literal(label), b -> action.run()).bounds(x, y, w, h).build());
    }

    private String read(String key, String fallback)
    {
        try
        {
            File file = settingsFile();
            if (file == null || !file.isFile()) return fallback;
            String text = readAll(file);
            Matcher m = (key.equals("username") ? USER : MEM).matcher(text);
            return m.find() ? m.group(1) : fallback;
        }
        catch (Throwable t)
        {
            return fallback;
        }
    }

    private void save()
    {
        try
        {
            File file = settingsFile();
            if (file == null) { status = "找不到综合包目录"; return; }

            String name = user.getValue().trim().replaceAll("[^A-Za-z0-9_]", "");
            if (name.length() < 3) name = "P_" + name;
            if (name.length() > 16) name = name.substring(0, 16);
            if (name.isEmpty()) name = "Steve";

            int mem = 4096;
            try { mem = Integer.parseInt(memory.getValue().trim()); } catch (Throwable ignored) { }
            if (mem < 512) mem = 512;
            if (mem > 32768) mem = 32768;

            try (PrintWriter out = new PrintWriter(new FileWriter(file)))
            {
                out.println("{");
                out.println("  \"username\": \"" + name + "\",");
                out.println("  \"memoryMb\": " + mem);
                out.println("}");
            }

            status = "已保存：" + name + " · " + mem + " MB";
            Diag.log("settings saved: " + name + " " + mem);
        }
        catch (Throwable t)
        {
            Diag.log("settings.save", t);
            status = "保存失败：" + t.getMessage();
        }
    }

    private File settingsFile()
    {
        File root = MinecraftPaths.packageRoot();
        return root == null ? null : new File(root, "PCLOne-settings.json");
    }

    private static String readAll(File file) throws Exception
    {
        java.io.ByteArrayOutputStream out = new java.io.ByteArrayOutputStream();
        java.io.FileInputStream in = new java.io.FileInputStream(file);
        try
        {
            byte[] buf = new byte[4096];
            int n;
            while ((n = in.read(buf)) > 0) out.write(buf, 0, n);
        }
        finally { in.close(); }
        return new String(out.toByteArray(), java.nio.charset.StandardCharsets.UTF_8);
    }

    @Override
    public void extractRenderState(GuiGraphicsExtractor graphics, int mouseX, int mouseY, float partialTick)
    {
        super.extractRenderState(graphics, mouseX, mouseY, partialTick);
        try
        {
            graphics.text(this.font, this.title, (this.width - this.font.width(this.title)) / 2, 24, 0xFFFFFFFF);
            graphics.text(this.font, Component.literal("离线用户名"), this.width / 2 - 100, 48, 0xFF9FD3A8);
            graphics.text(this.font, Component.literal("内存（MB，512–32768）"), this.width / 2 - 100, 94, 0xFF9FD3A8);
            if (status != null) graphics.text(this.font, Component.literal(status), this.width / 2 - 100, 176, 0xFFFFFF00);
        }
        catch (Throwable t)
        {
            Diag.log("settings.render", t);
        }
    }

    @Override
    public void onClose()
    {
        Minecraft.getInstance().setScreenAndShow(new PclOneScreen(0));
    }
}
