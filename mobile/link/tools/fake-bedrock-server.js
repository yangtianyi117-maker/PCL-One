// Minimal fake Minecraft Bedrock server that answers RakNet unconnected pings (0x01 -> 0x1C).
// Used to verify the app's protocol parser without a real game server.
const dgram = require('dgram');

const port = parseInt(process.argv[2] || '19132', 10);
const name = process.argv[3] || 'PCL Pro Test Room';
const players = parseInt(process.argv[4] || '3', 10);
const maxPlayers = parseInt(process.argv[5] || '10', 10);
const version = process.argv[6] || '1.21.8';

const MAGIC = Buffer.from([0x00, 0xff, 0xff, 0x00, 0xfe, 0xfe, 0xfe, 0xfe, 0xfd, 0xfd, 0xfd, 0xfd, 0x12, 0x34, 0x56, 0x78]);

const server = dgram.createSocket('udp4');

server.on('message', (msg, rinfo) => {
  if (msg.length < 33 || msg[0] !== 0x01) return;
  const pingTime = msg.readBigInt64BE(1);
  const payload = [
    'MCPE',
    name,
    '800',
    version,
    String(players),
    String(maxPlayers),
    '123456789',
    'PCLProTest',
    'Survival',
    '1',
    String(port),
    String(port)
  ].join(';');

  const body = Buffer.alloc(payload.length);
  body.write(payload, 'utf8');

  const out = Buffer.alloc(1 + 8 + 8 + 16 + 2 + body.length);
  let o = 0;
  out.writeUInt8(0x1c, o); o += 1;
  out.writeBigInt64BE(pingTime, o); o += 8;
  out.writeBigInt64BE(9876543210n, o); o += 8;
  MAGIC.copy(out, o); o += 16;
  out.writeUInt16BE(body.length, o); o += 2;
  body.copy(out, o);

  server.send(out, rinfo.port, rinfo.address);
});

server.bind(port, '0.0.0.0', () => {
  console.log(`fake bedrock server on 0.0.0.0:${port} name="${name}" ${players}/${maxPlayers} ${version}`);
});
