package pcl.one;

import java.io.File;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiGraphicsExtractor;
import net.minecraft.client.gui.components.Button;
import net.minecraft.client.gui.screens.ConnectScreen;
import net.minecraft.client.gui.screens.Screen;
import net.minecraft.client.multiplayer.ServerData;
import net.minecraft.client.multiplayer.resolver.ServerAddress;
import net.minecraft.network.chat.Component;

/**
 * One screen, no sub-pages: every installed version (paged) plus the fixed actions.
 * Any "&lt;root&gt;/.minecraft/versions/&lt;id&gt;/&lt;id&gt;.json" under the package is listed,
 * so adding a Fabric/Forge instance needs no code change.
 */
public final class PclOneScreen extends Screen
{
    private static final String HOST = "124.221.231.213";
    private static final int PORT = 25566;
    private static final int PAGE_SIZE = 4;

    private final List<VersionEntry> versions = new ArrayList<>();
    private final int page;
    private String status;

    public PclOneScreen()
    {
        this(0);
    }

    public PclOneScreen(int page)
    {
        super(Minecraft.getInstance(), Minecraft.getInstance().font, Component.literal("PCL One · 版本与工具"));
        this.page = page;
    }

    @Override
    protected void init()
    {
        try
        {
            scanVersions();
            build();
        }
        catch (Throwable t)
        {
            Diag.log("screen.init", t);
        }
    }

    private void build()
    {
        int cx = this.width / 2;
        int top = 52;
        int w = 208;
        int h = 20;
        int step = 24;
        int start = page * PAGE_SIZE;
        int end = Math.min(versions.size(), start + PAGE_SIZE);

        for (int i = start; i < end; i++)
        {
            VersionEntry entry = versions.get(i);
            int idx = i - start;
            int x = idx % 2 == 0 ? cx - 214 : cx + 6;
            int y = top + (idx / 2) * step;
            add(x, y, w, h, shorten(entry.label), () -> launchVersion(entry));
        }

        int actions = top + 2 * step + 6;
        add(cx - 214, actions, 208, h, "进入地球（在线）", this::joinEarth);
        add(cx + 6, actions, 208, h, "服务器列表", () -> Minecraft.getInstance().setScreenAndShow(new ServerScreen(0)));
        add(cx - 214, actions + step, 208, h, "开泰坦服", () -> launch("server"));
        add(cx + 6, actions + step, 208, h, "离线自检", () -> launch("selftest"));
        add(cx - 214, actions + step * 2, 208, h, "设置（用户名/内存）", () -> Minecraft.getInstance().setScreenAndShow(new SettingsScreen()));
        add(cx + 6, actions + step * 2, 208, h, "打开综合包目录", this::openFolder);

        int nav = actions + 3 * step;
        if (page > 0) add(cx - 214, nav, 100, h, "上一页", () -> Minecraft.getInstance().setScreenAndShow(new PclOneScreen(page - 1)));
        add(cx - 50, nav, 100, h, "刷新", () -> Minecraft.getInstance().setScreenAndShow(new PclOneScreen(page)));
        if (end < versions.size()) add(cx + 114, nav, 100, h, "下一页", () -> Minecraft.getInstance().setScreenAndShow(new PclOneScreen(page + 1)));

        add(cx - 50, this.height - 34, 100, h, "关闭", this::onClose);
    }

    private void add(int x, int y, int width, int height, String label, Runnable action)
    {
        addRenderableWidget(Button.builder(Component.literal(label), b -> action.run())
            .bounds(x, y, width, height)
            .build());
    }

    private void scanVersions()
    {
        versions.clear();
        File root = MinecraftPaths.packageRoot();
        if (root == null)
        {
            status = "找不到综合包目录";
            return;
        }

        Map<String, File> roots = new LinkedHashMap<>();
        addRoot(roots, new File(root, "Earth/.minecraft"), "Earth");
        addRoot(roots, new File(root, "Titan/.minecraft"), "Titan");

        File[] children = root.listFiles();
        if (children != null)
        {
            for (File child : children)
            {
                if (!child.isDirectory()) continue;
                File mc = new File(child, ".minecraft");
                if (new File(mc, "versions").isDirectory()) addRoot(roots, mc, child.getName());
            }
        }

        try
        {
            File games = new File(root, "games.txt");
            if (games.isFile())
            {
                java.io.BufferedReader reader = new java.io.BufferedReader(new java.io.FileReader(games));
                try
                {
                    String line;
                    while ((line = reader.readLine()) != null)
                    {
                        line = line.trim();
                        if (line.isEmpty() || line.startsWith("#")) continue;
                        int bar = line.indexOf('|');
                        if (bar < 0) continue;
                        String label = line.substring(0, bar).trim();
                        String path = line.substring(bar + 1).trim();
                        File dir = new File(path);
                        if (!dir.isAbsolute()) dir = new File(root, path);
                        if (label.isEmpty()) label = dir.getName();
                        addRoot(roots, dir, label);
                    }
                }
                finally { reader.close(); }
            }
        }
        catch (Throwable ignored) { }

        for (Map.Entry<String, File> e : roots.entrySet())
        {
            File[] instances = new File(e.getValue(), "versions").listFiles();
            if (instances == null) continue;

            for (File instance : instances)
            {
                if (!instance.isDirectory()) continue;
                String id = instance.getName();
                if (!new File(instance, id + ".json").isFile()) continue;
                versions.add(new VersionEntry(e.getKey() + " · " + id, relative(root, e.getValue()), id));
            }
        }

        Diag.log("scan: " + versions.size() + " versions under " + root);
    }

    private void addRoot(Map<String, File> roots, File gameDir, String label)
    {
        if (new File(gameDir, "versions").isDirectory()) roots.put(label, gameDir);
    }

    private String relative(File root, File file)
    {
        String r = root.getAbsolutePath();
        String f = file.getAbsolutePath();
        if (f.startsWith(r)) f = f.substring(r.length());
        return f.replace('/', '\\').replaceAll("^\\\\+", "");
    }

    private String shorten(String text)
    {
        if (this.font.width(text) <= 200) return text;
        String cut = text;
        while (cut.length() > 4 && this.font.width(cut + "…") > 200) cut = cut.substring(0, cut.length() - 1);
        return cut + "…";
    }

    private void launchVersion(VersionEntry entry)
    {
        File root = MinecraftPaths.packageRoot();
        File exe = root == null ? null : new File(root, "PCL One.exe");
        if (exe == null || !exe.isFile())
        {
            status = "找不到 PCL One.exe";
            Diag.log("launch-version: no exe, root=" + root);
            return;
        }

        try
        {
            new ProcessBuilder(exe.getAbsolutePath(), "launch", entry.gameRel, entry.instance)
                .directory(root)
                .start();
            status = "已启动：" + entry.instance;
            Diag.log("launch-version: " + entry.gameRel + " " + entry.instance);
        }
        catch (Throwable t)
        {
            Diag.log("launch-version", t);
            status = "启动失败：" + t.getMessage();
        }
    }

    private void launch(String argument)
    {
        File root = MinecraftPaths.packageRoot();
        if (root == null) { status = "找不到综合包目录"; return; }

        File exe = new File(root, "PCL One.exe");
        if (!exe.isFile()) { status = "找不到 PCL One.exe：" + exe; return; }

        try
        {
            new ProcessBuilder(exe.getAbsolutePath(), argument).directory(root).start();
            status = "已启动：" + argument;
            Diag.log("launch: " + argument);
        }
        catch (Throwable t)
        {
            Diag.log("launch-" + argument, t);
            status = "启动失败：" + t.getMessage();
        }
    }

    private void joinEarth()
    {
        try
        {
            Minecraft mc = Minecraft.getInstance();
            ServerAddress address = new ServerAddress(HOST, PORT);
            ServerData data = new ServerData("Earth 1:1000", HOST + ":" + PORT, ServerData.Type.OTHER);
            ConnectScreen.startConnecting(this, mc, address, data, false, null);
            status = "正在连接 " + HOST + ":" + PORT;
        }
        catch (Throwable t)
        {
            Diag.log("join-earth", t);
            status = "连接失败：" + t;
        }
    }

    private void openFolder()
    {
        File root = MinecraftPaths.packageRoot();
        if (root == null) { status = "找不到综合包目录"; return; }
        try
        {
            new ProcessBuilder("explorer.exe", root.getAbsolutePath()).start();
            status = "已打开：" + root;
        }
        catch (Throwable t)
        {
            Diag.log("open-folder", t);
            status = "打开失败：" + t.getMessage();
        }
    }

    @Override
    public void extractRenderState(GuiGraphicsExtractor graphics, int mouseX, int mouseY, float partialTick)
    {
        super.extractRenderState(graphics, mouseX, mouseY, partialTick);

        try
        {
            String title = this.title.getString();
            graphics.text(this.font, Component.literal(title), (this.width - this.font.width(title)) / 2, 18, 0xFFFFFFFF);

            String hint = "发现 " + versions.size() + " 个版本 · 任意已安装版本均可启动";
            graphics.text(this.font, Component.literal(hint), (this.width - this.font.width(hint)) / 2, 34, 0xFF9FD3A8);

            if (status != null) graphics.text(this.font, Component.literal(status), 20, this.height - 52, 0xFFFFFF00);
        }
        catch (Throwable t)
        {
            Diag.log("screen.render", t);
        }
    }

    private static final class VersionEntry
    {
        final String label;
        final String gameRel;
        final String instance;

        VersionEntry(String label, String gameRel, String instance)
        {
            this.label = label;
            this.gameRel = gameRel;
            this.instance = instance;
        }
    }
}
