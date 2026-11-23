package com.pclpro.mobile;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.Context;
import android.content.Intent;
import android.graphics.PorterDuff;
import android.net.Uri;
import android.net.wifi.WifiManager;
import android.os.Bundle;
import android.text.InputType;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.ProgressBar;
import android.widget.ScrollView;
import android.widget.SeekBar;
import android.widget.TextView;
import android.widget.Toast;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

public class MainActivity extends Activity implements ServerScanner.Listener {

    private Prefs prefs;
    private ServerScanner scanner;

    private final ArrayList<ServerInfo> servers = new ArrayList<>();
    private ServerInfo selected;

    private int tab = 0;
    private FrameLayout content;
    private final TextView[] navLabels = new TextView[3];
    private final IconView[] navIcons = new IconView[3];

    private LinearLayout serverListBox;
    private LinearLayout emptyBox;
    private LinearLayout scanStatus;
    private ProgressBar scanProgress;
    private TextView scanStatusText;
    private TextView scanButton;

    private TextView officialStatus;
    private TextView officialDetail;
    private TextView officialRefresh;

    private boolean scanning;
    private int scanDone;
    private int scanTotal;
    private int scanFound;

    private WifiManager.MulticastLock multicastLock;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        prefs = new Prefs(this);
        scanner = new ServerScanner();

        for (ServerInfo s : prefs.loadServers()) servers.add(s);
        String saved = prefs.serverAddr();
        for (ServerInfo s : servers) {
            if (s.address().equals(saved)) {
                selected = s;
                break;
            }
        }

        getWindow().setStatusBarColor(Ui.BLUE_DARK);
        getWindow().setNavigationBarColor(0xFFFFFFFF);

        buildChrome();
        selectTab(0);
        handleDeepLink(getIntent());
    }

    @Override
    protected void onResume() {
        super.onResume();
        refreshOfficial();
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        setIntent(intent);
        handleDeepLink(intent);
    }

    /** Handles pclpro://join?... and pclpro://room?... invitations produced by the desktop. */
    private void handleDeepLink(Intent intent) {
        if (intent == null) return;
        Uri data = intent.getData();
        if (data == null || !"pclpro".equalsIgnoreCase(data.getScheme())) return;

        String code = data.getQueryParameter("c");
        String network = data.getQueryParameter("n");
        String secret = data.getQueryParameter("s");
        String host = data.getQueryParameter("h");
        String port = data.getQueryParameter("p");

        boolean imported = false;
        if (code != null && !code.trim().isEmpty()) {
            prefs.setRoomCode(code.trim());
            imported = true;
        }
        if (network != null && !network.trim().isEmpty()) {
            prefs.setRoomNetwork(network.trim());
            imported = true;
        }
        if (secret != null && !secret.trim().isEmpty()) {
            prefs.setRoomSecret(secret.trim());
            imported = true;
        }
        if (host != null && !host.trim().isEmpty()) {
            prefs.setRoomHost(host.trim());
            imported = true;
        }
        if (port != null && !port.trim().isEmpty()) {
            try {
                prefs.setRoomPort(Integer.parseInt(port.trim()));
            } catch (Exception ignored) {
            }
        }

        if (imported) {
            selectTab(1);
            toast("已导入 PCL Pro 联机邀请");
        }
    }

    // ------------------------------------------------------------------ chrome

    private void buildChrome() {
        LinearLayout root = Ui.column(this);
        root.setBackgroundColor(Ui.BG);
        root.setFitsSystemWindows(true);

        LinearLayout header = Ui.row(this);
        header.setBackgroundColor(Ui.BLUE);
        header.setPadding(d(16), d(10), d(14), d(10));

        IconView logo = new IconView(this).type(IconView.CUBE).tint(Ui.WHITE);
        LinearLayout.LayoutParams logoLp = new LinearLayout.LayoutParams(d(28), d(28));
        logoLp.rightMargin = d(10);
        header.addView(logo, logoLp);

        LinearLayout titles = Ui.column(this);
        titles.addView(Ui.tv(this, "PCL Pro", 21, Ui.WHITE, true));
        titles.addView(Ui.tv(this, "手机版 · 联机助手", 11, 0xFFCFE0F7, false));
        header.addView(titles, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));

        header.addView(Ui.chip(this, "Bedrock", 0x33FFFFFF, Ui.WHITE));

        root.addView(header, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        content = new FrameLayout(this);
        content.setBackgroundColor(Ui.BG);
        root.addView(content, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, 0, 1));

        View line = new View(this);
        line.setBackgroundColor(0xFFE6ECF5);
        root.addView(line, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, 1));

        LinearLayout nav = Ui.row(this);
        nav.setBackgroundColor(0xFFFFFFFF);
        nav.setPadding(0, d(6), 0, d(6));
        String[] labels = {"大厅", "房间", "设置"};
        int[] icons = {IconView.GRID, IconView.HOUSE, IconView.GEAR};
        for (int i = 0; i < 3; i++) {
            final int index = i;
            LinearLayout item = Ui.column(this);
            item.setGravity(Gravity.CENTER);
            item.setPadding(0, d(2), 0, d(2));
            item.setClickable(true);
            item.setOnClickListener(new View.OnClickListener() {
                @Override
                public void onClick(View v) {
                    selectTab(index);
                }
            });
            IconView icon = new IconView(this).type(icons[i]).tint(Ui.SUB);
            item.addView(icon, new LinearLayout.LayoutParams(d(22), d(22)));
            TextView label = Ui.tv(this, labels[i], 10, Ui.SUB, false);
            LinearLayout.LayoutParams llp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            llp.topMargin = d(3);
            item.addView(label, llp);
            navIcons[i] = icon;
            navLabels[i] = label;
            nav.addView(item, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        }
        root.addView(nav, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        setContentView(root);
    }

    private void selectTab(int index) {
        tab = index;
        for (int i = 0; i < 3; i++) {
            boolean on = i == index;
            navIcons[i].tint(on ? Ui.BLUE : Ui.SUB);
            navLabels[i].setTextColor(on ? Ui.BLUE : Ui.SUB);
            navLabels[i].setTypeface(android.graphics.Typeface.create(
                    "sans-serif-medium", android.graphics.Typeface.NORMAL));
        }
        content.removeAllViews();
        View view;
        if (index == 0) view = buildLobby();
        else if (index == 1) view = buildRoom();
        else view = buildSettings();
        content.addView(view, new FrameLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT));
    }

    // ------------------------------------------------------------------ lobby

    private View buildLobby() {
        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        LinearLayout root = Ui.column(this);
        root.setPadding(0, d(4), 0, d(12));

        root.addView(buildOfficialCard());

        LinearLayout card = Ui.card(this);

        LinearLayout titleRow = Ui.row(this);
        titleRow.addView(Ui.tv(this, "局域网大厅", 17, Ui.TEXT, true),
                new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        titleRow.addView(Ui.chip(this, "Bedrock", Ui.BLUE_SOFT, Ui.BLUE));
        card.addView(titleRow);

        TextView subtitle = Ui.tv(this,
                "自动发现同一 Wi-Fi 下的 PCL Pro 房间和 Minecraft 服务器",
                12, Ui.SUB, false);
        LinearLayout.LayoutParams subLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        subLp.topMargin = d(4);
        card.addView(subtitle, subLp);

        LinearLayout actions = Ui.row(this);
        LinearLayout.LayoutParams actionsLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        actionsLp.topMargin = d(12);
        card.addView(actions, actionsLp);

        scanButton = Ui.button(this, "扫描局域网", true);
        scanButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                startScan();
            }
        });
        actions.addView(scanButton, new LinearLayout.LayoutParams(0, d(44), 1));

        TextView addButton = Ui.button(this, "手动添加", false);
        LinearLayout.LayoutParams addLp = new LinearLayout.LayoutParams(0, d(44), 1);
        addLp.leftMargin = d(8);
        addButton.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                showAddDialog();
            }
        });
        actions.addView(addButton, addLp);

        scanStatus = Ui.column(this);
        LinearLayout.LayoutParams scanLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        scanLp.topMargin = d(10);
        card.addView(scanStatus, scanLp);

        scanProgress = new ProgressBar(this, null, android.R.attr.progressBarStyleHorizontal);
        scanProgress.setIndeterminate(true);
        scanProgress.setMax(100);
        try {
            scanProgress.getIndeterminateDrawable().setColorFilter(Ui.BLUE, PorterDuff.Mode.SRC_IN);
        } catch (Exception ignored) {
        }
        scanStatus.addView(scanProgress, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, d(4)));

        scanStatusText = Ui.tv(this, "", 12, Ui.SUB, false);
        LinearLayout.LayoutParams stLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        stLp.topMargin = d(8);
        scanStatus.addView(scanStatusText, stLp);

        serverListBox = Ui.column(this);
        LinearLayout.LayoutParams listLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        listLp.topMargin = d(6);
        card.addView(serverListBox, listLp);

        emptyBox = Ui.column(this);
        emptyBox.setGravity(Gravity.CENTER);
        emptyBox.setPadding(0, d(22), 0, d(18));
        IconView big = new IconView(this).type(IconView.WIFI).tint(0xFFC3D0E0);
        emptyBox.addView(big, new LinearLayout.LayoutParams(d(44), d(44)));
        TextView emptyTitle = Ui.tv(this, "还没有发现服务器", 14, Ui.TEXT, true);
        LinearLayout.LayoutParams etLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        etLp.topMargin = d(8);
        emptyBox.addView(emptyTitle, etLp);
        TextView emptyHint = Ui.tv(this,
                "在 PCL Pro 里创建房间，手机和电脑连同一个 Wi-Fi，再点「扫描局域网」。",
                12, Ui.SUB, false);
        emptyHint.setGravity(Gravity.CENTER);
        LinearLayout.LayoutParams ehLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        ehLp.topMargin = d(5);
        emptyBox.addView(emptyHint, ehLp);
        card.addView(emptyBox);

        root.addView(card);

        // room code quick card
        LinearLayout roomCard = Ui.card(this);
        LinearLayout roomTitle = Ui.row(this);
        roomTitle.addView(Ui.tv(this, "PCL Pro 房间号", 15, Ui.TEXT, true),
                new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        roomTitle.addView(Ui.chip(this, "联机", 0xFFE9F7EF, Ui.GREEN));
        roomCard.addView(roomTitle);

        String code = prefs.roomCode();
        TextView codeView = Ui.tv(this, code.isEmpty() ? "未设置" : code, 17,
                code.isEmpty() ? Ui.SUB : Ui.BLUE, true);
        LinearLayout.LayoutParams cvLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        cvLp.topMargin = d(8);
        roomCard.addView(codeView, cvLp);

        LinearLayout roomActions = Ui.row(this);
        LinearLayout.LayoutParams raLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        raLp.topMargin = d(10);
        roomCard.addView(roomActions, raLp);
        TextView copyInvite = Ui.button(this, "复制邀请", false);
        copyInvite.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                copy(inviteText());
                toast("邀请已复制");
            }
        });
        roomActions.addView(copyInvite, new LinearLayout.LayoutParams(0, d(40), 1));
        TextView goRoom = Ui.button(this, "去房间页", false);
        LinearLayout.LayoutParams grLp = new LinearLayout.LayoutParams(0, d(40), 1);
        grLp.leftMargin = d(8);
        goRoom.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                selectTab(1);
            }
        });
        roomActions.addView(goRoom, grLp);

        root.addView(roomCard);

        LinearLayout tips = Ui.card(this);
        tips.addView(Ui.tv(this, "进服小贴士", 15, Ui.TEXT, true));
        String[] lines = {
                "电脑端 PCL Pro 联机页点「创建房间」",
                "手机与电脑连接同一个 Wi-Fi（不要开 AP 隔离）",
                "服务器端口默认 19132，对应 Geyser 通道",
                "找不到电脑时用「手动添加」填电脑 IP",
                "手机需已安装 Minecraft Bedrock 版"
        };
        for (String line : lines) {
            TextView t = Ui.tv(this, "·  " + line, 12, Ui.SUB, false);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            lp.topMargin = d(6);
            tips.addView(t, lp);
        }
        root.addView(tips);

        scroll.addView(root);
        refreshServers();
        updateScanUi();
        return scroll;
    }

    private View buildOfficialCard() {
        LinearLayout card = Ui.card(this);

        LinearLayout titleRow = Ui.row(this);
        titleRow.addView(Ui.tv(this, "地球服", 17, Ui.TEXT, true),
                new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        titleRow.addView(Ui.chip(this, "推荐", 0xFFE9F7EF, Ui.GREEN));
        officialRefresh = Ui.tv(this, "刷新", 12, Ui.BLUE, true);
        officialRefresh.setPadding(d(10), d(4), d(4), d(4));
        officialRefresh.setClickable(true);
        officialRefresh.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                refreshOfficial();
            }
        });
        LinearLayout.LayoutParams rfLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        rfLp.leftMargin = d(8);
        titleRow.addView(officialRefresh, rfLp);
        card.addView(titleRow);

        TextView subtitle = Ui.tv(this,
                "一张真实地球地图 · PC Java 与手机 Bedrock 同世界",
                12, Ui.SUB, false);
        LinearLayout.LayoutParams subLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        subLp.topMargin = d(4);
        card.addView(subtitle, subLp);

        officialStatus = Ui.tv(this, "检测中…", 14, Ui.SUB, true);
        LinearLayout.LayoutParams stLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        stLp.topMargin = d(10);
        card.addView(officialStatus, stLp);

        officialDetail = Ui.tv(this, "手机 " + prefs.officialBedrock()
                + "  ·  PC " + prefs.officialJava(), 11, Ui.SUB, false);
        officialDetail.setMaxLines(2);
        LinearLayout.LayoutParams dtLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        dtLp.topMargin = d(4);
        card.addView(officialDetail, dtLp);

        LinearLayout actions = Ui.row(this);
        LinearLayout.LayoutParams acLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        acLp.topMargin = d(12);
        card.addView(actions, acLp);

        TextView enter = Ui.button(this, "进入", true);
        enter.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                enterOfficial();
            }
        });
        actions.addView(enter, new LinearLayout.LayoutParams(0, d(44), 1));

        TextView copyBedrock = Ui.button(this, "复制手机地址", false);
        LinearLayout.LayoutParams cbLp = new LinearLayout.LayoutParams(0, d(44), 1);
        cbLp.leftMargin = d(8);
        copyBedrock.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                copy(prefs.officialBedrock());
                toast("已复制 " + prefs.officialBedrock());
            }
        });
        actions.addView(copyBedrock, cbLp);

        TextView copyJava = Ui.button(this, "复制 PC 地址", false);
        LinearLayout.LayoutParams cjLp = new LinearLayout.LayoutParams(0, d(44), 1);
        cjLp.leftMargin = d(8);
        copyJava.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                copy(prefs.officialJava());
                toast("已复制 " + prefs.officialJava());
            }
        });
        actions.addView(copyJava, cjLp);

        TextView guide = Ui.button(this, "地球指南（规则 / 坐标）", false);
        LinearLayout.LayoutParams glLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, d(40));
        glLp.topMargin = d(8);
        guide.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                openGuide();
            }
        });
        card.addView(guide, glLp);

        return card;
    }

    private void openGuide() {
        // Offline first: the guide ships inside the APK (assets/earth.html).
        try {
            final android.webkit.WebView web = new android.webkit.WebView(this);
            web.getSettings().setJavaScriptEnabled(false);
            web.setBackgroundColor(0xFF0F1216);
            web.loadUrl("file:///android_asset/" + OfficialServers.GUIDE_ASSET);
            new AlertDialog.Builder(this)
                    .setTitle("地球指南 · 离线")
                    .setView(web)
                    .setPositiveButton("关闭", null)
                    .setNeutralButton("用浏览器打开", new android.content.DialogInterface.OnClickListener() {
                        @Override
                        public void onClick(android.content.DialogInterface dialog, int which) {
                            openGuideOnline();
                        }
                    })
                    .show();
            return;
        } catch (Throwable ignored) {
            // fall through to the online guide
        }
        openGuideOnline();
    }

    private void openGuideOnline() {
        try {
            startActivity(new Intent(Intent.ACTION_VIEW, Uri.parse(OfficialServers.GUIDE_URL)));
        } catch (Exception e) {
            toast("没有可用浏览器，地址：" + OfficialServers.GUIDE_URL);
        }
    }

    private void refreshOfficial() {
        if (officialStatus == null) return;
        officialStatus.setText("检测中…");
        officialStatus.setTextColor(Ui.SUB);
        officialRefresh.setText("检测中…");
        final String bedrock = prefs.officialBedrock();
        final String java = prefs.officialJava();
        new Thread(new Runnable() {
            @Override
            public void run() {
                final RakNetPing.Result bedrockResult = RakNetPing.ping(
                        OfficialServers.hostOf(bedrock),
                        OfficialServers.portOf(bedrock, OfficialServers.BEDROCK_PORT),
                        1800);
                final JavaPing.Result javaResult = JavaPing.ping(
                        OfficialServers.hostOf(java),
                        OfficialServers.portOf(java, OfficialServers.JAVA_PORT),
                        2500);
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        updateOfficialUi(bedrockResult, javaResult);
                    }
                });
            }
        }, "pclpro-official").start();
    }

    private void updateOfficialUi(RakNetPing.Result bedrock, JavaPing.Result java) {
        if (officialStatus == null) return;
        if (bedrock != null) {
            String players = bedrock.players >= 0 && bedrock.maxPlayers >= 0
                    ? " · " + bedrock.players + "/" + bedrock.maxPlayers + " 人"
                    : "";
            officialStatus.setText("在线" + players + " · " + bedrock.pingMs + " ms");
            officialStatus.setTextColor(Ui.GREEN);
        } else {
            officialStatus.setText("离线 / 无响应（仍可尝试进入）");
            officialStatus.setTextColor(Ui.ORANGE);
        }

        StringBuilder detail = new StringBuilder();
        detail.append("手机 ").append(prefs.officialBedrock());
        if (bedrock != null && bedrock.version != null && !bedrock.version.isEmpty()) {
            detail.append("（").append(bedrock.version).append("）");
        }
        detail.append("\nPC ").append(prefs.officialJava());
        if (java != null) {
            detail.append("（").append(java.version.isEmpty() ? "在线" : java.version);
            if (java.players >= 0 && java.maxPlayers >= 0) {
                detail.append(" ").append(java.players).append("/").append(java.maxPlayers);
            }
            detail.append("）");
        } else {
            detail.append("（离线 / 无响应）");
        }
        officialDetail.setText(detail.toString());
        officialRefresh.setText("刷新");
        officialRefresh.setTextColor(Ui.BLUE);
    }

    private void enterOfficial() {
        ServerInfo server = parseAddress(prefs.officialBedrock(), OfficialServers.BEDROCK_PORT);
        if (server == null) {
            toast("官方服地址无效，请检查设置");
            return;
        }
        server.name = OfficialServers.NAME + " · Bedrock";
        selectAndOpen(server);
    }

    private void refreshServers() {
        if (serverListBox == null) return;
        serverListBox.removeAllViews();

        List<ServerInfo> sorted = new ArrayList<>(servers);
        Collections.sort(sorted, new Comparator<ServerInfo>() {
            @Override
            public int compare(ServerInfo a, ServerInfo b) {
                long pa = a.pingMs < 0 ? Long.MAX_VALUE : a.pingMs;
                long pb = b.pingMs < 0 ? Long.MAX_VALUE : b.pingMs;
                if (pa != pb) return pa < pb ? -1 : 1;
                return a.name.compareToIgnoreCase(b.name);
            }
        });

        for (int i = 0; i < sorted.size(); i++) {
            if (i > 0) {
                View div = new View(this);
                div.setBackgroundColor(Ui.LINE);
                LinearLayout.LayoutParams dlp = new LinearLayout.LayoutParams(
                        ViewGroup.LayoutParams.MATCH_PARENT, 1);
                dlp.topMargin = d(8);
                dlp.bottomMargin = d(8);
                serverListBox.addView(div, dlp);
            }
            serverListBox.addView(buildServerRow(sorted.get(i)),
                    new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT,
                            ViewGroup.LayoutParams.WRAP_CONTENT));
        }
        if (emptyBox != null) {
            emptyBox.setVisibility(sorted.isEmpty() && !scanning ? View.VISIBLE : View.GONE);
        }
    }

    private View buildServerRow(final ServerInfo s) {
        LinearLayout row = Ui.row(this);
        row.setPadding(d(6), d(10), d(4), d(10));
        row.setBackground(Ui.pressable(0xFFF7FAFF, 10, this));
        row.setClickable(true);
        row.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                showServerDialog(s);
            }
        });
        row.setOnLongClickListener(new View.OnLongClickListener() {
            @Override
            public boolean onLongClick(View v) {
                if (!s.manual) {
                    toast("长按可删除手动添加的服务器");
                    return true;
                }
                new AlertDialog.Builder(MainActivity.this)
                        .setTitle("删除服务器")
                        .setMessage("确定删除 " + s.address() + " 吗？")
                        .setPositiveButton("删除", new android.content.DialogInterface.OnClickListener() {
                            @Override
                            public void onClick(android.content.DialogInterface dialog, int which) {
                                servers.remove(s);
                                prefs.saveServers(servers);
                                refreshServers();
                                toast("已删除");
                            }
                        })
                        .setNegativeButton("取消", null)
                        .show();
                return true;
            }
        });

        LinearLayout iconBox = Ui.column(this);
        iconBox.setGravity(Gravity.CENTER);
        iconBox.setBackground(Ui.round(Ui.BLUE_SOFT, 10, this));
        iconBox.addView(new IconView(this).type(IconView.CUBE).tint(Ui.BLUE),
                new LinearLayout.LayoutParams(d(24), d(24)));
        row.addView(iconBox, new LinearLayout.LayoutParams(d(42), d(42)));

        LinearLayout info = Ui.column(this);
        LinearLayout.LayoutParams infoLp = new LinearLayout.LayoutParams(0,
                ViewGroup.LayoutParams.WRAP_CONTENT, 1);
        infoLp.leftMargin = d(10);
        infoLp.rightMargin = d(6);
        row.addView(info, infoLp);

        TextView name = Ui.tv(this, s.name, 15, Ui.TEXT, true);
        name.setMaxLines(1);
        name.setEllipsize(android.text.TextUtils.TruncateAt.END);
        info.addView(name);

        TextView addr = Ui.tv(this, s.address() + "  ·  " + s.subtitle(), 11.5f, Ui.SUB, false);
        addr.setMaxLines(1);
        addr.setEllipsize(android.text.TextUtils.TruncateAt.END);
        LinearLayout.LayoutParams addrLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        addrLp.topMargin = d(3);
        info.addView(addr, addrLp);

        LinearLayout right = Ui.column(this);
        right.setGravity(Gravity.CENTER);
        TextView ping = Ui.tv(this, s.pingText(), 10.5f, s.pingColor(), true);
        ping.setGravity(Gravity.CENTER);
        right.addView(ping);

        TextView enter = Ui.tv(this, "进入", 12, Ui.BLUE, true);
        enter.setGravity(Gravity.CENTER);
        enter.setPadding(d(12), d(5), d(12), d(5));
        enter.setBackground(Ui.pressable(Ui.BLUE_SOFT, 14, this));
        enter.setClickable(true);
        enter.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                selectAndOpen(s);
            }
        });
        LinearLayout.LayoutParams enterLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, d(30));
        enterLp.topMargin = d(5);
        right.addView(enter, enterLp);

        row.addView(right, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        return row;
    }

    // ------------------------------------------------------------------ room

    private View buildRoom() {
        final ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        LinearLayout root = Ui.column(this);
        root.setPadding(0, d(4), 0, d(12));

        LinearLayout card = Ui.card(this);
        LinearLayout titleRow = Ui.row(this);
        titleRow.addView(Ui.tv(this, "我的房间", 17, Ui.TEXT, true),
                new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        titleRow.addView(Ui.chip(this, "PCL Pro", Ui.BLUE_SOFT, Ui.BLUE));
        card.addView(titleRow);

        TextView roomLabel = Ui.tv(this, "房间号", 12, Ui.SUB, false);
        LinearLayout.LayoutParams rlLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        rlLp.topMargin = d(10);
        card.addView(roomLabel, rlLp);

        final EditText roomInput = Ui.field(this, "U/R4YN-L7KP-A7Z2-JL9B");
        roomInput.setText(prefs.roomCode());
        LinearLayout.LayoutParams riLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        riLp.topMargin = d(5);
        card.addView(roomInput, riLp);

        TextView addrLabel = Ui.tv(this, "服务器地址", 12, Ui.SUB, false);
        LinearLayout.LayoutParams alLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        alLp.topMargin = d(10);
        card.addView(addrLabel, alLp);

        final EditText addrInput = Ui.field(this, "192.168.1.5:19132");
        addrInput.setText(prefs.serverAddr());
        LinearLayout.LayoutParams aiLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        aiLp.topMargin = d(5);
        card.addView(addrInput, aiLp);

        LinearLayout buttons = Ui.row(this);
        LinearLayout.LayoutParams bLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        bLp.topMargin = d(12);
        card.addView(buttons, bLp);

        TextView save = Ui.button(this, "保存", true);
        save.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                String code = roomInput.getText().toString().trim();
                String addr = addrInput.getText().toString().trim();
                prefs.setRoomCode(code);
                prefs.setServerAddr(addr);
                ServerInfo parsed = parseAddress(addr, prefs.port());
                if (parsed != null) {
                    selected = parsed;
                    Toast.makeText(MainActivity.this, "已保存", Toast.LENGTH_SHORT).show();
                    selectTab(1);
                } else if (!addr.isEmpty()) {
                    toast("地址格式应为 IP:端口");
                } else {
                    toast("已保存房间号");
                }
            }
        });
        buttons.addView(save, new LinearLayout.LayoutParams(0, d(44), 1));

        TextView share = Ui.button(this, "复制邀请", false);
        LinearLayout.LayoutParams shLp = new LinearLayout.LayoutParams(0, d(44), 1);
        shLp.leftMargin = d(8);
        share.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                prefs.setRoomCode(roomInput.getText().toString().trim());
                prefs.setServerAddr(addrInput.getText().toString().trim());
                copy(inviteText());
                toast("邀请已复制");
            }
        });
        buttons.addView(share, shLp);

        TextView hint = Ui.tv(this, "房间号在 PCL Pro 建房后获得，例如 U/R4YN-L7KP-A7Z2-JL9B。",
                11, Ui.SUB, false);
        LinearLayout.LayoutParams hlLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        hlLp.topMargin = d(8);
        card.addView(hint, hlLp);

        final TextView roomInfo = Ui.tv(this, roomInfoText(), 11.5f, Ui.SUB, false);
        roomInfo.setLineSpacing(d(2), 1.0f);
        LinearLayout.LayoutParams ri2Lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        ri2Lp.topMargin = d(8);
        card.addView(roomInfo, ri2Lp);

        LinearLayout roomActions = Ui.row(this);
        LinearLayout.LayoutParams raLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        raLp.topMargin = d(10);
        card.addView(roomActions, raLp);

        TextView parseRoom = Ui.button(this, "解析房间码", false);
        parseRoom.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                String raw = roomInput.getText().toString().trim();
                String[] derived = deriveRoom(raw);
                if (derived == null) {
                    toast("房间码格式应为 U/XXXX-XXXX-XXXX-XXXX");
                    return;
                }
                prefs.setRoomCode(derived[0]);
                prefs.setRoomNetwork(derived[1]);
                prefs.setRoomSecret(derived[2]);
                roomInput.setText(derived[0]);
                if (addrInput.getText().toString().trim().isEmpty()) {
                    String addr = prefs.roomHost() + ":" + prefs.roomPort();
                    addrInput.setText(addr);
                    prefs.setServerAddr(addr);
                }
                roomInfo.setText(roomInfoText());
                toast("已解析：网络 " + derived[1]);
            }
        });
        roomActions.addView(parseRoom, new LinearLayout.LayoutParams(0, d(42), 1));

        TextView copyConfig = Ui.button(this, "复制 EasyTier 配置", false);
        LinearLayout.LayoutParams ccLp = new LinearLayout.LayoutParams(0, d(42), 1);
        ccLp.leftMargin = d(8);
        copyConfig.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                ensureRoomDerived();
                copy(roomEasyTierText());
                toast("EasyTier 配置已复制");
            }
        });
        roomActions.addView(copyConfig, ccLp);

        LinearLayout roomActions2 = Ui.row(this);
        LinearLayout.LayoutParams ra2Lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        ra2Lp.topMargin = d(8);
        card.addView(roomActions2, ra2Lp);

        TextView openEt = Ui.button(this, "打开 EasyTier", false);
        openEt.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                openEasyTier();
            }
        });
        roomActions2.addView(openEt, new LinearLayout.LayoutParams(0, d(42), 1));

        TextView enterRoom = Ui.button(this, "进入基岩服", true);
        LinearLayout.LayoutParams erLp = new LinearLayout.LayoutParams(0, d(42), 1);
        erLp.leftMargin = d(8);
        enterRoom.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                ensureRoomDerived();
                String addr = prefs.roomHost() + ":" + prefs.roomPort();
                ServerInfo s = parseAddress(addr, 19132);
                if (s == null) {
                    toast("房间地址无效");
                    return;
                }
                s.name = "PCL 房间 · Bedrock";
                selectAndOpen(s);
            }
        });
        roomActions2.addView(enterRoom, erLp);

        root.addView(card);

        LinearLayout steps = Ui.card(this);
        steps.addView(Ui.tv(this, "手机进服（Bedrock）", 17, Ui.TEXT, true));
        String[] stepTexts = {
                "地球服：回大厅点地球服「进入」，手机走 Bedrock 19132",
                "PC 端：PCL Pro「联机」页点地球服「进入」，走 Java 25566",
                "同 Wi-Fi：PC 联机页启动基岩桥，游戏里「对局域网开放」，回大厅扫描",
                "房间码：把电脑上的 U/ 码填到上面，按提示用 EasyTier 加入同一网络",
                "进入地址显示在「当前服务器」；Minecraft → 服务器 → 添加服务器"
        };
        for (int i = 0; i < stepTexts.length; i++) {
            LinearLayout row = stepsStep(i + 1, stepTexts[i]);
            LinearLayout.LayoutParams slp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            slp.topMargin = d(9);
            steps.addView(row, slp);
        }
        root.addView(steps);

        LinearLayout current = Ui.card(this);
        current.addView(Ui.tv(this, "当前服务器", 15, Ui.TEXT, true));
        final ServerInfo cur = selected != null ? selected : parseAddress(prefs.serverAddr(), prefs.port());
        TextView big = Ui.tv(this, cur != null ? cur.address() : "尚未选择服务器",
                21, cur != null ? Ui.BLUE : Ui.SUB, true);
        LinearLayout.LayoutParams bigLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        bigLp.topMargin = d(8);
        current.addView(big, bigLp);

        if (cur != null) {
            TextView meta = Ui.tv(this, cur.subtitle() + "  ·  " + cur.pingText(), 12, Ui.SUB, false);
            LinearLayout.LayoutParams mLp = new LinearLayout.LayoutParams(
                    ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
            mLp.topMargin = d(4);
            current.addView(meta, mLp);
        }

        LinearLayout curButtons = Ui.row(this);
        LinearLayout.LayoutParams cbLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        cbLp.topMargin = d(12);
        current.addView(curButtons, cbLp);

        TextView copyAddr = Ui.button(this, "复制地址", false);
        copyAddr.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if (cur == null) {
                    toast("先在「大厅」里选择服务器，或填写服务器地址");
                    return;
                }
                copy(cur.address());
                toast("已复制 " + cur.address());
            }
        });
        curButtons.addView(copyAddr, new LinearLayout.LayoutParams(0, d(44), 1));

        TextView openMc = Ui.button(this, "打开 Minecraft", true);
        LinearLayout.LayoutParams omLp = new LinearLayout.LayoutParams(0, d(44), 1);
        omLp.leftMargin = d(8);
        openMc.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if (cur == null) {
                    toast("先选择服务器再打开游戏");
                    return;
                }
                selectAndOpen(cur);
            }
        });
        curButtons.addView(openMc, omLp);
        root.addView(current);

        LinearLayout notes = Ui.card(this);
        notes.addView(Ui.tv(this, "说明", 15, Ui.TEXT, true));
        notes.addView(noteLine("手机版使用 Bedrock 协议，通过电脑端的 Geyser 端口进服。"));
        notes.addView(noteLine("Java 版客户端无法直接在手机上运行，游戏仍需在电脑上启动。"));
        notes.addView(noteLine("跨版本互通只对原版房间生效，带模组的房间需同版本同加载器。"));
        root.addView(notes);

        scroll.addView(root);
        return scroll;
    }

    private LinearLayout stepsStep(int n, String text) {
        LinearLayout row = Ui.row(this);
        TextView num = Ui.tv(this, String.valueOf(n), 11, Ui.WHITE, true);
        num.setGravity(Gravity.CENTER);
        num.setBackground(Ui.round(Ui.BLUE, 20, this));
        LinearLayout.LayoutParams np = new LinearLayout.LayoutParams(d(20), d(20));
        np.rightMargin = d(10);
        row.addView(num, np);
        TextView t = Ui.tv(this, text, 12.5f, Ui.TEXT, false);
        row.addView(t, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        return row;
    }

    private TextView noteLine(String text) {
        TextView t = Ui.tv(this, "·  " + text, 12, Ui.SUB, false);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = d(6);
        t.setLayoutParams(lp);
        return t;
    }

    // ------------------------------------------------------------------ settings

    private View buildSettings() {
        ScrollView scroll = new ScrollView(this);
        scroll.setFillViewport(true);
        LinearLayout root = Ui.column(this);
        root.setPadding(0, d(4), 0, d(12));

        LinearLayout card = Ui.card(this);
        card.addView(Ui.tv(this, "扫描设置", 17, Ui.TEXT, true));

        TextView portLabel = Ui.tv(this, "服务器端口", 12, Ui.SUB, false);
        LinearLayout.LayoutParams plLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        plLp.topMargin = d(10);
        card.addView(portLabel, plLp);

        final EditText portInput = Ui.field(this, "19132");
        portInput.setInputType(InputType.TYPE_CLASS_NUMBER);
        portInput.setText(String.valueOf(prefs.port()));
        LinearLayout.LayoutParams piLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        piLp.topMargin = d(5);
        card.addView(portInput, piLp);

        TextView timeoutLabel = Ui.tv(this, "扫描超时  " + prefs.timeoutMs() + " ms", 12, Ui.SUB, false);
        LinearLayout.LayoutParams tlLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        tlLp.topMargin = d(12);
        card.addView(timeoutLabel, tlLp);

        SeekBar seek = new SeekBar(this);
        seek.setMax(1300);
        seek.setProgress(Math.max(0, prefs.timeoutMs() - 200));
        seek.getProgressDrawable().setColorFilter(Ui.BLUE, PorterDuff.Mode.SRC_IN);
        seek.getThumb().setColorFilter(Ui.BLUE, PorterDuff.Mode.SRC_IN);
        final TextView timeoutValue = timeoutLabel;
        seek.setOnSeekBarChangeListener(new SeekBar.OnSeekBarChangeListener() {
            @Override
            public void onProgressChanged(SeekBar seekBar, int progress, boolean fromUser) {
                timeoutValue.setText("扫描超时  " + (progress + 200) + " ms");
            }

            @Override
            public void onStartTrackingTouch(SeekBar seekBar) {
            }

            @Override
            public void onStopTrackingTouch(SeekBar seekBar) {
            }
        });
        card.addView(seek, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        TextView save = Ui.button(this, "保存设置", true);
        LinearLayout.LayoutParams svLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, d(44));
        svLp.topMargin = d(12);
        save.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                int port = 19132;
                try {
                    port = Integer.parseInt(portInput.getText().toString().trim());
                } catch (Exception ignored) {
                }
                if (port < 1 || port > 65535) port = 19132;
                prefs.setPort(port);
                prefs.setTimeout(seek.getProgress() + 200);
                toast("设置已保存");
            }
        });
        card.addView(save, svLp);
        root.addView(card);

        LinearLayout about = Ui.card(this);
        about.addView(Ui.tv(this, "关于", 17, Ui.TEXT, true));
        about.addView(infoRow("名称", "PCL Pro 手机版"));
        about.addView(infoRow("版本", "1.1.0 (build 2)"));
        about.addView(infoRow("包名", "com.pclpro.mobile"));
        about.addView(infoRow("协议", "Minecraft Bedrock / RakNet 0x1C"));
        about.addView(infoRow("发现", "UDP 子网扫描 + 224.0.2.60 局域网广播"));
        about.addView(infoRow("官方手机", prefs.officialBedrock()));
        about.addView(infoRow("官方 PC", prefs.officialJava()));
        about.addView(infoRow("本机", NetUtil.displayAddress()));
        root.addView(about);

        LinearLayout diag = Ui.card(this);
        diag.addView(Ui.tv(this, "诊断", 17, Ui.TEXT, true));
        diag.addView(noteLine("扫描不到电脑时，先确认手机与电脑在同一网段，再检查电脑防火墙是否放行 UDP 19132。"));
        TextView copyDiag = Ui.button(this, "复制诊断信息", false);
        LinearLayout.LayoutParams cdLp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, d(44));
        cdLp.topMargin = d(10);
        copyDiag.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                String diagText = "PCL Pro 手机版 1.1.0\n"
                        + "官方手机: " + prefs.officialBedrock() + "\n"
                        + "官方 PC: " + prefs.officialJava() + "\n"
                        + "本机地址: " + NetUtil.displayAddress() + "\n"
                        + "扫描网段: " + NetUtil.subnetLabel() + "\n"
                        + "端口: " + prefs.port() + "\n"
                        + "超时: " + prefs.timeoutMs() + " ms\n"
                        + "已发现: " + servers.size() + " 台";
                copy(diagText);
                toast("诊断信息已复制");
            }
        });
        diag.addView(copyDiag, cdLp);
        root.addView(diag);

        scroll.addView(root);
        return scroll;
    }

    private LinearLayout infoRow(String key, String value) {
        LinearLayout row = Ui.row(this);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = d(8);
        row.setLayoutParams(lp);
        TextView k = Ui.tv(this, key, 12, Ui.SUB, false);
        k.setWidth(d(64));
        row.addView(k);
        TextView v = Ui.tv(this, value, 12, Ui.TEXT, false);
        row.addView(v, new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1));
        return row;
    }

    // ------------------------------------------------------------------ scan

    private void startScan() {
        if (scanning) return;
        scanning = true;
        scanDone = 0;
        scanTotal = 0;
        scanFound = 0;
        updateScanUi();
        acquireMulticastLock();
        scanner.startAnnounce(this, prefs.port());
        scanner.scan(prefs.port(), prefs.timeoutMs(), this);
    }

    private void updateScanUi() {
        if (scanButton == null) return;
        scanButton.setText(scanning ? "扫描中…" : "扫描局域网");
        scanButton.setEnabled(!scanning);
        if (scanStatus != null) {
            scanStatus.setVisibility(scanning || scanFound > 0 ? View.VISIBLE : View.GONE);
            scanProgress.setVisibility(scanning ? View.VISIBLE : View.GONE);
            if (scanning) {
                scanStatusText.setText("正在扫描 " + NetUtil.subnetLabel()
                        + "  ·  " + scanDone + "/" + scanTotal
                        + "  ·  已发现 " + scanFound + " 台");
            } else {
                scanStatusText.setText("扫描完成  ·  发现 " + scanFound + " 台服务器");
            }
        }
        if (emptyBox != null) {
            emptyBox.setVisibility(servers.isEmpty() && !scanning ? View.VISIBLE : View.GONE);
        }
    }

    @Override
    public void onScanStart(int total) {
        scanTotal = total;
        scanDone = 0;
        updateScanUi();
    }

    @Override
    public void onScanFound(ServerInfo server, int done, int total) {
        // The same server can answer on more than one address (multi-homed host, NAT aliasing).
        // Keep one row per server GUID, preferring the fastest reply.
        if (!server.guid.isEmpty()) {
            for (ServerInfo existing : servers) {
                if (server.guid.equals(existing.guid)) {
                    if (server.pingMs >= 0 && (existing.pingMs < 0 || server.pingMs < existing.pingMs)) {
                        existing.host = server.host;
                        existing.port = server.port;
                        existing.pingMs = server.pingMs;
                        existing.lastSeen = server.lastSeen;
                    }
                    refreshServers();
                    updateScanUi();
                    return;
                }
            }
        }
        removeDuplicate(server.address());
        servers.add(server);
        scanFound = servers.size();
        refreshServers();
        updateScanUi();
    }

    @Override
    public void onScanProgress(int done, int total) {
        scanDone = done;
        scanTotal = total;
        updateScanUi();
    }

    @Override
    public void onScanFinished(int found) {
        scanning = false;
        scanDone = scanTotal;
        scanFound = found;
        scanner.stopAnnounce();
        releaseMulticastLock();
        if (scanTotal > 0 && found == 0) {
            toast("没有发现服务器，确认电脑已建房且在同一 Wi-Fi");
        } else {
            toast("扫描完成，发现 " + found + " 台服务器");
        }
        updateScanUi();
        refreshServers();
    }

    @Override
    public void onAnnounce(ServerInfo server) {
        if (servers.isEmpty() || find(server.address()) == null) {
            servers.add(server);
            scanFound = servers.size();
            refreshServers();
            updateScanUi();
        }
    }

    @Override
    public void onScanError(String message) {
        scanning = false;
        scanner.stopAnnounce();
        releaseMulticastLock();
        toast(message);
        updateScanUi();
    }

    private void removeDuplicate(String address) {
        for (int i = servers.size() - 1; i >= 0; i--) {
            if (servers.get(i).address().equals(address)) servers.remove(i);
        }
    }

    private ServerInfo find(String address) {
        for (ServerInfo s : servers) {
            if (s.address().equals(address)) return s;
        }
        return null;
    }

    // ------------------------------------------------------------------ actions

    private void selectAndOpen(ServerInfo s) {
        selected = s;
        prefs.setServerAddr(s.address());
        copy(s.address());
        toast("已复制地址 " + s.address());
        openMinecraft(s);
    }

    private void openMinecraft(ServerInfo s) {
        String[] uris = {
                "minecraft://connect?serverUrl=" + s.host + "&serverPort=" + s.port,
                "minecraft://?serverUrl=" + s.host + "&serverPort=" + s.port,
                "minecraft://"
        };
        for (String uri : uris) {
            try {
                Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(uri));
                intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                startActivity(intent);
                return;
            } catch (Exception ignored) {
            }
        }
        try {
            Intent launch = getPackageManager().getLaunchIntentForPackage("com.mojang.minecraftpe");
            if (launch != null) {
                launch.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                startActivity(launch);
                toast("已打开 Minecraft，请在「服务器」里粘贴地址");
                return;
            }
        } catch (Exception ignored) {
        }
        toast("已复制地址：Minecraft → 游戏 → 服务器 → 添加服务器");
    }

    private void showAddDialog() {
        LinearLayout box = Ui.column(this);
        box.setPadding(d(20), d(8), d(20), 0);

        final EditText addr = Ui.field(this, "192.168.1.5");
        box.addView(addr, new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT));

        final EditText port = Ui.field(this, "19132");
        port.setInputType(InputType.TYPE_CLASS_NUMBER);
        port.setText(String.valueOf(prefs.port()));
        LinearLayout.LayoutParams plp = new LinearLayout.LayoutParams(
                ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        plp.topMargin = d(10);
        box.addView(port, plp);

        new AlertDialog.Builder(this)
                .setTitle("手动添加服务器")
                .setView(box)
                .setPositiveButton("添加", new android.content.DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(android.content.DialogInterface dialog, int which) {
                        String host = addr.getText().toString().trim();
                        int p = prefs.port();
                        try {
                            p = Integer.parseInt(port.getText().toString().trim());
                        } catch (Exception ignored) {
                        }
                        if (host.isEmpty()) {
                            toast("请输入地址");
                            return;
                        }
                        addManual(host, p);
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    private void addManual(final String host, final int port) {
        removeDuplicate(host + ":" + port);
        final ServerInfo s = new ServerInfo();
        s.name = host;
        s.host = host;
        s.port = port;
        s.manual = true;
        servers.add(s);
        prefs.saveServers(servers);
        refreshServers();

        new Thread(new Runnable() {
            @Override
            public void run() {
                final RakNetPing.Result r = RakNetPing.ping(host, port, 1200);
                runOnUiThread(new Runnable() {
                    @Override
                    public void run() {
                        if (r != null) {
                            s.name = r.displayName();
                            s.version = r.version;
                            s.motd = r.motd;
                            s.players = r.players;
                            s.maxPlayers = r.maxPlayers;
                            s.pingMs = r.pingMs;
                            s.lastSeen = System.currentTimeMillis();
                            prefs.saveServers(servers);
                            refreshServers();
                        } else {
                            toast("已添加，但暂时连不上 " + host + ":" + port);
                        }
                    }
                });
            }
        }).start();
    }

    private void showServerDialog(final ServerInfo s) {
        String msg = "地址：" + s.address() + "\n"
                + "版本：" + (s.version.isEmpty() ? "未知" : s.version) + "\n"
                + "人数：" + s.playersText() + "\n"
                + "延迟：" + s.pingText();
        if (!s.motd.isEmpty() && !s.motd.equals(s.name)) msg += "\nMOTD：" + s.motd;
        new AlertDialog.Builder(this)
                .setTitle(s.name)
                .setMessage(msg)
                .setPositiveButton("进入", new android.content.DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(android.content.DialogInterface dialog, int which) {
                        selectAndOpen(s);
                    }
                })
                .setNeutralButton("复制地址", new android.content.DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(android.content.DialogInterface dialog, int which) {
                        copy(s.address());
                        toast("已复制");
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }

    private String inviteText() {
        String code = prefs.roomCode();
        String addr = prefs.serverAddr();
        if (addr.isEmpty() && selected != null) addr = selected.address();
        StringBuilder sb = new StringBuilder();
        sb.append("加入我的 PCL Pro 房间\n");
        if (!code.isEmpty()) sb.append("房间号：").append(code).append("\n");
        if (!addr.isEmpty()) sb.append("服务器：").append(addr).append("\n");
        sb.append("手机 Minecraft → 游戏 → 服务器 → 添加服务器，填上面的地址。");
        return sb.toString();
    }

    /** Normalizes a U/ code and derives the EasyTier network name / secret (same as PCL CE). */
    private String[] deriveRoom(String raw) {
        if (raw == null) return null;
        String text = raw.trim().toUpperCase().replace(" ", "").replace("\t", "");
        text = text.trim().replace("\"", "").replace("'", "");
        if (text.startsWith("U/")) text = text.substring(2);
        text = text.replace("-", "");
        if (text.length() != 16) return null;
        for (int i = 0; i < text.length(); i++) {
            char c = text.charAt(i);
            boolean ok = (c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z');
            if (!ok) return null;
        }
        String payload = text.substring(0, 4) + "-" + text.substring(4, 8) + "-"
                + text.substring(8, 12) + "-" + text.substring(12, 16);
        String network = "scaffolding-mc-" + payload.substring(0, 9);
        String secret = payload.substring(10);
        return new String[]{"U/" + payload, network, secret};
    }

    private void ensureRoomDerived() {
        if (!prefs.roomNetwork().isEmpty() && !prefs.roomSecret().isEmpty()) return;
        String[] derived = deriveRoom(prefs.roomCode());
        if (derived != null) {
            prefs.setRoomCode(derived[0]);
            prefs.setRoomNetwork(derived[1]);
            prefs.setRoomSecret(derived[2]);
        }
    }

    private String roomInfoText() {
        ensureRoomDerived();
        String code = prefs.roomCode();
        String network = prefs.roomNetwork();
        String secret = prefs.roomSecret();
        if (code.isEmpty() && network.isEmpty()) {
            return "尚未填写房间码。电脑端建房后复制邀请，或手动输入 U/ 码后点「解析房间码」。";
        }
        StringBuilder sb = new StringBuilder();
        if (!code.isEmpty()) sb.append("房间码：").append(code).append("\n");
        if (!network.isEmpty()) sb.append("EasyTier 网络：").append(network).append("\n");
        if (!secret.isEmpty()) sb.append("EasyTier 密钥：").append(secret).append("\n");
        sb.append("手机进入：").append(prefs.roomHost()).append(":").append(prefs.roomPort());
        return sb.toString();
    }

    private String roomEasyTierText() {
        ensureRoomDerived();
        StringBuilder sb = new StringBuilder();
        sb.append("PCL Pro 联机房间\n");
        if (!prefs.roomCode().isEmpty()) sb.append("房间码：").append(prefs.roomCode()).append("\n");
        sb.append("EasyTier 网络：").append(prefs.roomNetwork()).append("\n");
        sb.append("EasyTier 密钥：").append(prefs.roomSecret()).append("\n");
        sb.append("手机进入：").append(prefs.roomHost()).append(":").append(prefs.roomPort()).append("\n");
        sb.append("步骤：在 EasyTier 里新建网络，填上面的网络名/密钥；连上后打开 Minecraft，\n");
        sb.append("在服务器里添加 ").append(prefs.roomHost()).append(":").append(prefs.roomPort()).append("。");
        return sb.toString();
    }

    private void openEasyTier() {
        String[] packages = {"com.easytier.app", "org.easytier.app", "com.kkrainbow.easytier"};
        for (String pkg : packages) {
            try {
                Intent intent = getPackageManager().getLaunchIntentForPackage(pkg);
                if (intent != null) {
                    intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                    startActivity(intent);
                    toast("已打开 EasyTier，请按复制的配置加入网络");
                    return;
                }
            } catch (Exception ignored) {
            }
        }
        try {
            Intent browser = new Intent(Intent.ACTION_VIEW,
                    Uri.parse("https://github.com/EasyTier/EasyTier/releases/latest"));
            browser.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
            startActivity(browser);
            toast("未安装 EasyTier，已打开官方下载页");
        } catch (Exception e) {
            toast("未安装 EasyTier；请先安装官方 APK");
        }
    }

    private ServerInfo parseAddress(String text, int fallbackPort) {
        if (text == null) return null;
        String t = text.trim();
        if (t.isEmpty()) return null;
        String host = t;
        int port = fallbackPort;
        int colon = t.lastIndexOf(':');
        if (colon > 0 && colon < t.length() - 1) {
            host = t.substring(0, colon);
            try {
                port = Integer.parseInt(t.substring(colon + 1));
            } catch (Exception e) {
                return null;
            }
        }
        if (host.isEmpty() || port < 1 || port > 65535) return null;
        ServerInfo s = new ServerInfo();
        s.host = host;
        s.port = port;
        s.name = host;
        s.manual = true;
        return s;
    }

    private void copy(String text) {
        try {
            ClipboardManager cm = (ClipboardManager) getSystemService(Context.CLIPBOARD_SERVICE);
            cm.setPrimaryClip(ClipData.newPlainText("PCL Pro", text));
        } catch (Exception ignored) {
        }
    }

    private void toast(String text) {
        Toast.makeText(this, text, Toast.LENGTH_SHORT).show();
    }

    private int d(int dp) {
        return Ui.dp(this, dp);
    }

    private void acquireMulticastLock() {
        try {
            WifiManager wm = (WifiManager) getApplicationContext().getSystemService(Context.WIFI_SERVICE);
            if (wm != null) {
                multicastLock = wm.createMulticastLock("pclpro-scan");
                multicastLock.setReferenceCounted(false);
                multicastLock.acquire();
            }
        } catch (Exception ignored) {
        }
    }

    private void releaseMulticastLock() {
        try {
            if (multicastLock != null) {
                multicastLock.release();
                multicastLock = null;
            }
        } catch (Exception ignored) {
        }
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        try {
            scanner.cancel();
            scanner.stopAnnounce();
        } catch (Exception ignored) {
        }
        releaseMulticastLock();
    }
}
