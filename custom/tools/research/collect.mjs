// 联机服务器搜集器：从公共目录抓地址，再实测 Java/Bedrock 在线状态。
// 用法: node tools/research/collect.mjs
import { writeFileSync, mkdirSync } from 'node:fs';
import net from 'node:net';
import dgram from 'node:dgram';

const OUT = 'docs/research/servers-collected.json';
const DOMAIN = /([a-zA-Z0-9][a-zA-Z0-9.-]{2,60}\.(?:net|com|org|io|gg|xyz|me|cn|top|cc|de|eu|us|co|site|online|fun|game|world|link|pro|dev|cloud|host|space|tech|store|shop))(?::(\d{2,5}))?/g;
const SKIP = /(google|gstatic|schema\\.org|discord|w3\\.org|fonts|jquery|cloudflare|mozilla|github|twitter|youtube|imgur|^\s*(www|wiki|document|consent|api|cdn|static)\\.)/i;

async function getText(url) {
  const res = await fetch(url, { headers: { 'user-agent': 'Mozilla/5.0 (research)' } });
  if (!res.ok) throw new Error(url + ' -> ' + res.status);
  return await res.text();
}

function extract(html, source, kind, trust = false) {
  const out = new Map();
  for (const m of html.matchAll(DOMAIN)) {
    const host = m[1].toLowerCase();
    if (SKIP.test(host)) continue;
    if (host === 'find-minecraft-servers.net' || host.endsWith('minecraft.buzz')) continue;
    if (!trust && !/^(play|mc|join|bedrock|buzz|bee|hub|smp)|craft|minecraft/i.test(host)) continue;
    let port = m[2] ? Number(m[2]) : (kind === 'bedrock' ? 19132 : 25565);
    let realKind = kind;
    if (/^(bedrock|buzz|bee)\./i.test(host) && kind === 'java') { realKind = 'bedrock'; port = 19132; }
    const key = host + ':' + port;
    if (!out.has(key)) out.set(key, { host, port, kind: realKind, source });
  }
  return [...out.values()];
}

function readVarInt(buf, off) {
  let num = 0, shift = 0, i = off;
  while (i < buf.length) {
    const b = buf[i++];
    num |= (b & 0x7f) << shift;
    if ((b & 0x80) === 0) break;
    shift += 7;
  }
  return { value: num >>> 0, size: i - off };
}

function writeVarInt(n) {
  const bytes = [];
  let v = n;
  do { let b = v & 0x7f; v >>>= 7; if (v !== 0) b |= 0x80; bytes.push(b); } while (v !== 0);
  return Buffer.from(bytes);
}

function mcString(s) {
  const b = Buffer.from(s, 'utf8');
  return Buffer.concat([writeVarInt(b.length), b]);
}

function javaPing(host, port, timeout = 5200) {
  return new Promise((resolve) => {
    const started = Date.now();
    const socket = net.createConnection({ host, port });
    let done = false;
    const finish = (r) => { if (done) return; done = true; try { socket.destroy(); } catch {} resolve(r); };
    socket.setTimeout(timeout);
    socket.on('timeout', () => finish({ ok: false, error: 'timeout' }));
    socket.on('error', (e) => finish({ ok: false, error: e.code || e.message }));
    socket.on('connect', () => {
      const hostBuf = mcString(host);
      const body = Buffer.concat([
        writeVarInt(0x00), writeVarInt(0xffffffff), hostBuf,
        Buffer.from([port >> 8, port & 0xff]), writeVarInt(1),
      ]);
      socket.write(Buffer.concat([writeVarInt(body.length), body]));
      socket.write(Buffer.from([0x01, 0x00]));
    });
    let acc = Buffer.alloc(0);
    socket.on('data', (chunk) => {
      acc = Buffer.concat([acc, chunk]);
      if (acc.length < 5) return;
      const lenInfo = readVarInt(acc, 0);
      if (acc.length < lenInfo.size + lenInfo.value) return;
      let off = lenInfo.size;
      const idInfo = readVarInt(acc, off); off += idInfo.size;
      if (idInfo.value !== 0x00) return finish({ ok: false, error: 'bad packet' });
      const strLen = readVarInt(acc, off); off += strLen.size;
      const json = acc.slice(off, off + strLen.value).toString('utf8');
      try {
        const data = JSON.parse(json);
        finish({
          ok: true,
          latency: Date.now() - started,
          version: data.version?.name ?? '',
          online: data.players?.online ?? 0,
          max: data.players?.max ?? 0,
          motd: (data.description?.text ?? (typeof data.description === 'string' ? data.description : '')) || '',
        });
      } catch (e) {
        finish({ ok: false, error: 'json' });
      }
    });
  });
}

function bedrockPing(host, port, timeout = 5200) {
  return new Promise((resolve) => {
    const started = Date.now();
    const sock = dgram.createSocket('udp4');
    const magic = Buffer.from('00ffff00fefefefefdfdfdfd12345678', 'hex');
    const pkt = Buffer.alloc(33);
    pkt[0] = 0x01;
    pkt.writeBigUInt64BE(BigInt(Date.now()), 1);
    magic.copy(pkt, 9);
    pkt.writeBigUInt64BE(0x1234n, 25);
    let done = false;
    const finish = (r) => { if (done) return; done = true; try { sock.close(); } catch {} resolve(r); };
    const timer = setTimeout(() => finish({ ok: false, error: 'timeout' }), timeout);
    sock.on('error', (e) => { clearTimeout(timer); finish({ ok: false, error: e.code || e.message }); });
    sock.on('message', (msg) => {
      if (msg.length < 35 || msg[0] !== 0x1c) return;
      const len = msg.readUInt16BE(33);
      const body = msg.slice(35, 35 + len).toString('utf8');
      const parts = body.split(';');
      clearTimeout(timer);
      finish({ ok: true, latency: Date.now() - started, version: parts[3] ?? '', online: Number(parts[4] || 0), max: Number(parts[5] || 0), motd: parts[1] ?? '' });
    });
    sock.send(pkt, port, host, (err) => { if (err) { clearTimeout(timer); finish({ ok: false, error: err.code || String(err) }); } });
  });
}

function slug(host) { return host.replace(/[^a-z0-9]+/gi, '-').replace(/^-|-$/g, '').toLowerCase(); }

const sources = [
  { url: 'https://minecraft.buzz/bedrock', kind: 'bedrock', label: 'minecraft.buzz' },
  { url: 'https://find-minecraft-servers.net/', kind: 'java', label: 'find-minecraft-servers.net' },
  { url: 'https://minecraftservers.org/', kind: 'java', label: 'minecraftservers.org' },
  { url: 'https://minecraft.buzz/java', kind: 'java', label: 'minecraft.buzz' },
];

// mclists：/servers/online.html、online_2.html … （中文服，带 IP 端口）
for (let p = 1; p <= 8; p++) {
  sources.push({
    url: p === 1 ? 'https://www.mclists.cn/servers/online.html' : `https://www.mclists.cn/servers/online_${p}.html`,
    kind: 'java', label: 'mclists.cn', trust: true,
  });
}
// topg：?page=N
for (let p = 1; p <= 5; p++) {
  sources.push({
    url: p === 1 ? 'https://topg.org/minecraft-servers' : `https://topg.org/minecraft-servers?page=${p}`,
    kind: 'java', label: 'topg.org',
  });
}

const found = new Map();
for (const s of sources) {
  try {
    const html = await getText(s.url);
    for (const item of extract(html, s.label, s.kind, s.trust === true)) {
      if (!found.has(item.host)) found.set(item.host, item);
    }
    console.log(`[fetch] ${s.label}: ${found.size} hosts so far`);
  } catch (e) {
    console.error(`[fetch] ${s.label} failed: ${e.message}`);
  }
}

const targets = [...found.values()].slice(0, 260);
const results = [];

function hardTimeout(promise, ms) {
  return Promise.race([
    promise,
    new Promise((resolve) => setTimeout(() => resolve({ ok: false, error: 'hard-timeout' }), ms)),
  ]);
}

for (let i = 0; i < targets.length; i += 8) {
  const batch = targets.slice(i, i + 8);
  const probed = await Promise.all(batch.map(async (t) => {
    try {
      const run = () => (t.kind === 'bedrock' ? bedrockPing(t.host, t.port) : javaPing(t.host, t.port));
      let r = await hardTimeout(run(), 7000);
      if (!r.ok) r = await hardTimeout(run(), 7000);
      return { ...t, probe: r };
    } catch (e) {
      return { ...t, probe: { ok: false, error: e?.code || e?.message || 'error' } };
    }
  }));
  results.push(...probed);
  process.stderr.write(`probed ${Math.min(i + 8, targets.length)}/${targets.length}\n`);
}

const servers = results.map((r) => {
  const p = r.probe;
  return {
    id: slug(r.host),
    name: r.host.split('.')[0].replace(/^(play|mc|join|bedrock|buzz|bee|hub)[.-]?/i, '') || r.host,
    subtitle: p.ok ? `${p.version || '?'} · ${p.online}/${p.max}` : '未响应',
    description: p.ok ? (p.motd || r.host) : `探测失败：${p.error}`,
    tags: [r.kind === 'bedrock' ? '基岩' : 'Java', p.ok ? '在线' : '离线', r.source],
    mc: p.ok ? String(p.version).split(' ')[0] : '',
    loader: r.kind === 'bedrock' ? 'Bedrock' : 'Vanilla',
    java: r.kind === 'java' ? `${r.host}:${r.port}` : '',
    bedrock: r.kind === 'bedrock' ? `${r.host}:${r.port}` : '',
    source: r.source,
    verified: p.ok ? { online: p.online, max: p.max, latency: p.latency, version: p.version } : { error: p.error },
  };
});

servers.sort((a, b) => (b.verified.online ?? -1) - (a.verified.online ?? -1));
mkdirSync('docs/research', { recursive: true });
writeFileSync(OUT, JSON.stringify({ collectedAt: new Date().toISOString(), count: servers.length, servers }, null, 2), 'utf8');

const alive = servers.filter((s) => s.verified.online !== undefined).length;
console.log(`done: ${servers.length} servers, ${alive} online -> ${OUT}`);
for (const s of servers.slice(0, 15)) {
  console.log(`  ${s.verified.online !== undefined ? 'ON ' : 'off'} ${s.java || s.bedrock}  ${s.verified.online ?? '-'}/${s.verified.max ?? '-'}  ${s.verified.latency ?? '-'}ms`);
}
