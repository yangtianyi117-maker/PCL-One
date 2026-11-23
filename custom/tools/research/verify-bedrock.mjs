// 对 Bedrock 候选做 RakNet 实时探测，确认「目录里在线」是否真的可连。
// 用法: node tools/research/verify-bedrock.mjs [file] [n]
import { readFileSync, writeFileSync } from 'node:fs';
import dgram from 'node:dgram';

const file = process.argv[2] ?? 'docs/research/servers-beacon-bedrock.json';
const limit = Number(process.argv[3] ?? 300);
const list = JSON.parse(readFileSync(file, 'utf8')).servers.filter((s) => s.bedrock).slice(0, limit);

function ping(host, port, timeout = 4000) {
  return new Promise((resolve) => {
    const t0 = Date.now();
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
      const p = body.split(';');
      clearTimeout(timer);
      finish({ ok: true, latency: Date.now() - t0, version: p[3] ?? '', online: Number(p[4] || 0), max: Number(p[5] || 0), motd: p[1] ?? '' });
    });
    sock.send(pkt, port, host, (err) => { if (err) { clearTimeout(timer); finish({ ok: false, error: err.code || String(err) }); } });
  });
}

const results = [];
for (let i = 0; i < list.length; i += 8) {
  const batch = list.slice(i, i + 8);
  const probed = await Promise.all(batch.map(async (s) => {
    const [host, port] = s.bedrock.split(':');
    const r = await ping(host, Number(port ?? 19132));
    return { bedrock: s.bedrock, name: s.name, listed: s.verified?.online ?? 0, probe: r };
  }));
  results.push(...probed);
  process.stderr.write(`probed ${Math.min(i + 8, list.length)}/${list.length}\n`);
}

const alive = results.filter((r) => r.probe.ok);
const report = {
  verifiedAt: new Date().toISOString(),
  sample: results.length,
  alive: alive.length,
  aliveRate: results.length ? +(alive.length / results.length).toFixed(3) : 0,
  items: results,
};
writeFileSync('docs/research/verify-bedrock.json', JSON.stringify(report, null, 2), 'utf8');

console.log(`bedrock sample=${report.sample} alive=${report.alive} rate=${report.aliveRate}`);
for (const r of alive.slice(0, 10)) console.log(`  ${r.bedrock.padEnd(32)} listed=${String(r.listed).padEnd(6)} live=${r.probe.online}/${r.probe.max} ${r.probe.latency}ms`);
