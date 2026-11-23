// 从总库里挑「有人在线」的 Java 服做实时复核，按延迟分档，产出可展示的候选。
// 用法: node tools/research/curate.mjs [top=300]
import { readFileSync, writeFileSync } from 'node:fs';
import net from 'node:net';

const TOP = Number(process.argv[2] ?? 300);
const all = JSON.parse(readFileSync('docs/research/servers-all.json', 'utf8')).servers;

function wv(n) { const b = []; let v = n; do { let x = v & 0x7f; v >>>= 7; if (v) x |= 0x80; b.push(x); } while (v); return Buffer.from(b); }
function ms(s) { const b = Buffer.from(s); return Buffer.concat([wv(b.length), b]); }
function rv(buf, o) { let n = 0, s = 0, i = o; while (i < buf.length) { const b = buf[i++]; n |= (b & 0x7f) << s; if (!(b & 0x80)) break; s += 7; } return { value: n >>> 0, size: i - o }; }

function javaPing(host, port, timeout = 5000) {
  return new Promise((resolve) => {
    const t0 = Date.now();
    const s = net.createConnection({ host, port });
    let done = false;
    const finish = (r) => { if (done) return; done = true; try { s.destroy(); } catch {} resolve(r); };
    const hard = setTimeout(() => finish({ ok: false, error: 'timeout' }), timeout);
    s.on('error', (e) => { clearTimeout(hard); finish({ ok: false, error: e.code || e.message }); });
    s.on('connect', () => {
      const b = Buffer.concat([wv(0), wv(767), ms(host), Buffer.from([port >> 8, port & 255]), wv(1)]);
      s.write(Buffer.concat([wv(b.length), b]));
      s.write(Buffer.from([1, 0]));
    });
    let acc = Buffer.alloc(0);
    s.on('data', (c) => {
      acc = Buffer.concat([acc, c]);
      if (acc.length < 5) return;
      const L = rv(acc, 0);
      if (acc.length < L.size + L.value) return;
      let o = L.size; const id = rv(acc, o); o += id.size;
      const sl = rv(acc, o); o += sl.size;
      try {
        const j = JSON.parse(acc.slice(o, o + sl.value).toString());
        clearTimeout(hard);
        finish({ ok: true, latency: Date.now() - t0, online: j.players?.online ?? 0, max: j.players?.max ?? 0, version: j.version?.name ?? '' });
      } catch { clearTimeout(hard); finish({ ok: false, error: 'json' }); }
    });
  });
}

const candidates = all
  .filter((s) => s.java && (s.verified?.online ?? 0) > 0)
  .sort((a, b) => (b.verified.online ?? 0) - (a.verified.online ?? 0))
  .slice(0, TOP);

const rows = [];
for (let i = 0; i < candidates.length; i += 10) {
  const batch = candidates.slice(i, i + 10);
  const done = await Promise.all(batch.map(async (s) => {
    const [host, port] = s.java.split(':');
    const probe = await javaPing(host, Number(port ?? 25565));
    return { entry: s, probe };
  }));
  rows.push(...done);
  process.stderr.write(`probed ${Math.min(i + 10, candidates.length)}/${candidates.length}\n`);
}

const alive = rows.filter((r) => r.probe.ok);
const tier = (ms) => (ms < 300 ? 'A' : ms < 800 ? 'B' : 'C');
const tiers = { A: [], B: [], C: [] };
for (const r of alive) tiers[tier(r.probe.latency)].push(r);

const map = (r) => ({
  id: r.entry.id,
  name: r.entry.name,
  java: r.entry.java,
  version: r.probe.version || r.entry.verified?.version || '',
  online: r.probe.online,
  max: r.probe.max,
  latency: r.probe.latency,
  source: r.entry.source,
  description: r.entry.description,
});

const out = {
  curatedAt: new Date().toISOString(),
  probed: rows.length,
  alive: alive.length,
  aliveRate: rows.length ? +(alive.length / rows.length).toFixed(3) : 0,
  tiers: {
    A: tiers.A.sort((a, b) => b.probe.online - a.probe.online).map(map),
    B: tiers.B.sort((a, b) => b.probe.online - a.probe.online).map(map),
    C: tiers.C.sort((a, b) => b.probe.online - a.probe.online).map(map),
  },
  offline: rows.filter((r) => !r.probe.ok).map((r) => ({ java: r.entry.java, error: r.probe.error })),
};
writeFileSync('docs/research/servers-curated.json', JSON.stringify(out, null, 2), 'utf8');

console.log(`probed=${out.probed} alive=${out.alive} rate=${out.aliveRate} A=${tiers.A.length} B=${tiers.B.length} C=${tiers.C.length}`);
for (const r of [...tiers.A, ...tiers.B].sort((a, b) => b.probe.online - a.probe.online).slice(0, 12)) {
  console.log(`  [${tier(r.probe.latency)}] ${r.entry.java.padEnd(30)} ${r.probe.online}/${r.probe.max} ${r.probe.latency}ms`);
}
