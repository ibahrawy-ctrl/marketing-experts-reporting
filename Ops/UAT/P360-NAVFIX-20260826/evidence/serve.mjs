// خادم ساكن يقدّم بايتات `dist` المسحوبة من TEST كما هي (7/7 sha256 مطابقة) مع ارتداد SPA.
// النداءات إلى /api و/hubs لا تمرّ من هنا: يعترضها Playwright ويحوّلها إلى نفق TEST الحيّ.
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';

const DIST = '/tmp/p360nav/uatdist';
const PORT = 4420;
const MIME = {
  '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8', '.svg': 'image/svg+xml', '.png': 'image/png',
  '.json': 'application/json; charset=utf-8', '.woff2': 'font/woff2', '.ico': 'image/x-icon',
};

http.createServer((req, res) => {
  const rel = decodeURIComponent(req.url.split('?')[0]);
  let file = path.join(DIST, rel);
  if (!file.startsWith(DIST) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
    file = path.join(DIST, 'index.html');
  }
  res.writeHead(200, { 'Content-Type': MIME[path.extname(file)] || 'application/octet-stream' });
  fs.createReadStream(file).pipe(res);
}).listen(PORT, '127.0.0.1', () => console.log('p360nav uat harness on ' + PORT));
