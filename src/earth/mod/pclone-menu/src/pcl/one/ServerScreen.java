package pcl.one;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileReader;
import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphicsExtractor;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.network.chat.Component;

/** Server list from servers.txt: name|address|gameDir|instance (one per line). */
public final class ServerScreen extends Screen
{
    private static final int PAGE_SIZE = 6;

    private final List<String[]> servers = new ArrayList<>();
    private int page;
    private String status;

    public ServerScreen()
    {
        this(0);
    }

    public ServerScreen(int page)
    {
        super(Minecraft.getInstance(), Minecraft.getInstance().font, Component.literal("PCL One · 服务器"));
        this.page = page;
    }

    @Override
    protected void init()
    {
        load();

        int cx = this.width / 2;
        int start = page * PAGE_SIZE;
        int end = Math.min(servers.size(), start + PAGE_SIZE);

        for (int i = start; i < end; i++)
        {
            String[] row = servers.get(i);
            int y = 54 + (i - start) * 24;
            add(cx - 200, y, 400, 20, row[0] + "  ·  " + row[1], () -> connect(row));
        }

        int nav = 54 + PAGE_SIZE * 24 + 8;
        if (page > 0) add(cx - 200, nav, 90, 20, "上一页", () -> Minecraft.getInstance().setScreenAndShow(new ServerScreen(page - 1)));
        add(cx - 45, nav, 90, 20, "返回", () -> Minecraft.getInstance().setScreenAndShow(new PclOneScreen(0)));
        if (end < servers.size()) add(cx + 110, nav, 90, 20, "下一页", () -> Minecraft.getInstance().setScreenAndShow(new ServerScreen(page + 1)));
    }

    private void add(int x, int y, int w, int h, String label, Runnable action)
    {
        addRenderableWidget(Button.builder(Component.literal(label), b -> action.run()).bounds(x, y, w, h).build());
    }

    private void load()
    {
        servers.clear();
        File root = MinecraftPaths.packageRoot();
        if (root == null) { status = "找不到综合包目录"; return; }

        File file = new File(root, "servers.txt");
        if (!file.isFile()) { status = "缺少 servers.txt"; return; }

        try (BufferedReader reader = new BufferedReader(new FileReader(file)))
        {
            String line;
            while ((line = reader.readLine()) != null)
            {
                line = line.trim();
                if (line.isEmpty() || line.startsWith("#")) continue;
                String[] parts = line.split("\\|");
                if (parts.length < 4) continue;
                servers.add(new String[] { parts[0], parts[1], parts[2], parts[3] });
            }
        }
        catch (Throwable t)
        {
            Diag.log("servers.load", t);
            status = "读取失败：" + t.getMessage();
        }
    }

    private void connect(String[] row)
    {
        File root = MinecraftPaths.packageRoot();
        File exe = root == null ? null : new File(root, "PCL One.exe");
        if (exe == null || !exe.isFile()) { status = "找不到 PCL One.exe"; return; }

        try
        {
            new ProcessBuilder(exe.getAbsolutePath(), "launch", row[2], row[3], row[1])
                .directory(root)
                .start();
            status = "正在进入：" + row[0];
            Diag.log("server connect: " + row[0] + " " + row[1]);
        }
        catch (Throwable t)
        {
            Diag.log("server.connect", t);
            status = "启动失败：" + t.getMessage();
        }
    }

    @Override
    public void extractRenderState(GuiGraphicsExtractor graphics, int mouseX, int mouseY, float partialTick)
    {
        super.extractRenderState(graphics, mouseX, mouseY, partialTick);
        try
        {
            graphics.text(this.font, this.title, (this.width - this.font.width(this.title)) / 2, 24, 0xFFFFFFFF);
            String hint = "共 " + servers.size() + " 个服务器 · 点击直接启动并进入";
            graphics.text(this.font, Component.literal(hint), (this.width - this.font.width(hint)) / 2, 40, 0xFF9FD3A8);
            if (status != null) graphics.text(this.font, Component.literal(status), 20, this.height - 30, 0xFFFFFF00);
        }
        catch (Throwable t)
        {
            Diag.log("servers.render", t);
        }
    }

    @Override
    public void onClose()
    {
        Minecraft.getInstance().setScreenAndShow(new PclOneScreen(0));
    }
}
