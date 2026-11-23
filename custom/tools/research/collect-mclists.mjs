// mclists.cn 结构化 API 抓取：分页拉到指定数量，映射成 servers.json 同构条目。
// 用法: node tools/research/collect-mclists.mjs [pages=10] [limit=50]
import { writeFileSync } from 'node:fs';

const pages = Number(process.argv[2] ?? 10);
const limit = Number(process.argv[3] ?? 50);
const OUT = 'docs/research/servers-mclists.json';

const all = [];
for (let page = 1; page <= pages; page++) {
  const url = `https://www.mclists.cn/api/v2/servers?page=${page}&limit=${limit}`;
  try {
    const res = await fetch(url, { headers: { 'user-agent': 'Mozilla/5.0 (research)' } });
    if (!res.ok) throw new Error(`HTTP ${res.status}`);
    const json = await res.json();
    for (const item of json.data ?? []) all.push(item);
    process.stderr.write(`page ${page}/${pages}: +${(json.data ?? []).length} (total ${json.meta?.total ?? '?'})\n`);
  } catch (e) {
    process.stderr.write(`page ${page} failed: ${e.message}\n`);
  }
}

const seen = new Set();
const servers = [];
for (const item of all) {
  const endpoint = item.endpoint || (item.address ? `${item.address}:${item.port ?? 25565}` : '');
  if (!endpoint || seen.has(endpoint)) continue;
  seen.add(endpoint);

  const edition = String(item.edition ?? 'java').toLowerCase();
  const isBedrock = edition === 'bedrock';
  const isJava = edition === 'java' || edition === 'both';
  const players = item.players ?? {};

  servers.push({
    id: `mclists-${item.sid}`,
    name: item.name ?? endpoint,
    subtitle: `${item.version ?? '?'} · ${players.online ?? 0}/${players.maximum ?? 0}`,
    description: (item.description ?? '').replace(/\s+/g, ' ').trim(),
    tags: [item.country_code === 'CN' ? '国内' : '海外', isBedrock ? '基岩' : 'Java', item.category?.name ?? '综合'],
    mc: item.version ?? '',
    loader: isBedrock ? 'Bedrock' : 'Vanilla',
    java: isJava ? endpoint : '',
    bedrock: isBedrock ? endpoint : '',
    source: 'mclists.cn',
    verified: {
      online: players.online ?? 0,
      max: players.maximum ?? 0,
      status: item.status ?? '',
      version: item.version ?? '',
      updatedAt: item.updated_at ?? '',
    },
    url: item.url ?? '',
  });
}

servers.sort((a, b) => (b.verified.online ?? 0) - (a.verified.online ?? 0));
writeFileSync(OUT, JSON.stringify({ collectedAt: new Date().toISOString(), count: servers.length, servers }, null, 2), 'utf8');

const online = servers.filter((s) => s.verified.status === 'online').length;
const cn = servers.filter((s) => s.tags.includes('国内')).length;
console.log(`done: ${servers.length} servers (${online} online, ${cn} CN) -> ${OUT}`);
for (const s of servers.slice(0, 10)) {
  console.log(`  ${s.java || s.bedrock}  ${s.verified.online}/${s.verified.max}  ${s.verified.version}`);
}
