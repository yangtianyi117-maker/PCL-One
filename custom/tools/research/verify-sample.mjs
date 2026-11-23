// 抽样复核：对 servers-all.json 里「快照有玩家」的前 N 台做实时 Java ping，
// 检验采集数据的时效性。用法: node tools/research/verify-sample.mjs [n=40]
import { readFileSync, writeFileSync } from 'node:fs';
import net from 'node:net';

const N = Number(process.argv[2] ?? 40);
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
  .slice(0, N);

const results = [];
for (let i = 0; i < candidates.length; i += 6) {
  const batch = candidates.slice(i, i + 6);
  const probed = await Promise.all(batch.map(async (s) => {
    const [host, port] = s.java.split(':');
    return { java: s.java, snapshot: s.verified?.online ?? 0, probe: await javaPing(host, Number(port ?? 25565)) };
  }));
  results.push(...probed);
  process.stderr.write(`verified ${Math.min(i + 6, candidates.length)}/${candidates.length}\n`);
}

const alive = results.filter((r) => r.probe.ok);
const report = {
  verifiedAt: new Date().toISOString(),
  sample: results.length,
  alive: alive.length,
  aliveRate: results.length ? +(alive.length / results.length).toFixed(3) : 0,
  items: results,
};
writeFileSync('docs/research/verify-sample.json', JSON.stringify(report, null, 2), 'utf8');

console.log(`sample=${report.sample} alive=${report.alive} rate=${report.aliveRate}`);
for (const r of alive.slice(0, 10)) {
  console.log(`  ${r.java.padEnd(30)} snapshot=${String(r.snapshot).padEnd(5)} live=${r.probe.online}/${r.probe.max} ${r.probe.latency}ms`);
}
