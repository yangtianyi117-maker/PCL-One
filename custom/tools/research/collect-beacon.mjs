// beaconservers.com 的 markdown 端点解析：结构化程度高，适合批量收录。
// 用法: node tools/research/collect-beacon.mjs <java|bedrock> <pages> [out]
import { writeFileSync } from 'node:fs';

const section = process.argv[2] ?? 'bedrock';
const pages = Number(process.argv[3] ?? 24);
const startPage = Number(process.argv[5] ?? 1);
const OUT = process.argv[4] ?? `docs/research/servers-beacon-${section}.json`;

function parseBlock(block) {
  const name = (block.match(/^\[([^\]]+)\]/) ?? [])[1] ?? '';
  const addr = (block.match(/Server address:\s*(\S+)/) ?? [])[1] ?? '';
  const edition = (block.match(/Edition:\s*(\w+)/) ?? [])[1] ?? '';
  const status = (block.match(/Status:\s*(\w+)/) ?? [])[1] ?? '';
  const players = block.match(/Players:\s*(\d+)\s*\/\s*(\d+)/);
  const version = (block.match(/Version:\s*(.+)/) ?? [])[1]?.trim() ?? '';
  const desc = (block.match(/\n> (.+)/) ?? [])[1]?.trim() ?? '';
  if (!addr) return null;
  return {
    name: name || addr,
    address: addr,
    edition: edition.toLowerCase(),
    status: status.toLowerCase(),
    online: Number(players?.[1] ?? 0),
    max: Number(players?.[2] ?? 0),
    version,
    description: desc.replace(/\\\|/g, '|'),
  };
}

const all = [];
let totalPages = pages;
for (let page = startPage; page < startPage + pages; page++) {
  const url = `https://beaconservers.com/minecraft-servers/${section}.md?page=${page}`;
  try {
    const res = await fetch(url, { headers: { 'user-agent': 'Mozilla/5.0 (research)' } });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const text = await res.text();
    const meta = text.match(/Listing pages: \d+ \/ (\d+)/);
    if (meta) totalPages = Number(meta[1]);
    const blocks = text.split(/\n## /).slice(1);
    for (const block of blocks) {
      const item = parseBlock(block);
      if (item) all.push(item);
    }
    process.stderr.write(`page ${page}/${pages}: +${blocks.length} (total pages ${totalPages})\n`);
  } catch (e) {
    process.stderr.write(`page ${page} failed: ${e.message}\n`);
  }
}

// 去重（同地址保留人数多的）
const byAddr = new Map();
for (const item of all) {
  const prev = byAddr.get(item.address);
  if (!prev || item.online > prev.online) byAddr.set(item.address, item);
}

const servers = [...byAddr.values()].map((s, i) => {
  const isBedrock = s.edition === 'bedrock';
  return {
    id: `beacon-${i + 1}`,
    name: s.name,
    subtitle: `${s.version || '?'} · ${s.online}/${s.max}`,
    description: s.description,
    tags: [isBedrock ? '基岩' : 'Java', s.status === 'online' ? '在线' : '离线', 'beaconservers'],
    mc: s.version,
    loader: isBedrock ? 'Bedrock' : 'Vanilla',
    java: isBedrock ? '' : s.address,
    bedrock: isBedrock ? s.address : '',
    source: 'beaconservers.com',
    verified: { online: s.online, max: s.max, status: s.status, version: s.version },
  };
});

servers.sort((a, b) => (b.verified.online ?? 0) - (a.verified.online ?? 0));
writeFileSync(OUT, JSON.stringify({ collectedAt: new Date().toISOString(), section, availablePages: totalPages, count: servers.length, servers }, null, 2), 'utf8');

const online = servers.filter((s) => s.verified.status === 'online').length;
const bedrock = servers.filter((s) => s.bedrock).length;
console.log(`done: ${servers.length} unique (${online} online, ${bedrock} bedrock) -> ${OUT}`);
for (const s of servers.slice(0, 8)) console.log(`  ${s.java || s.bedrock}  ${s.verified.online}/${s.verified.max}`);
