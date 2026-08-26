#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""خادم محلّيّ يقدّم **حزمة RC المنشورة نفسها** (منسوخة كما هي من /opt/reporting-rc/frontend/dist)
ويمرّر /api و/hubs إلى خلفيّة RC الحيّة عبر نفق SSH على 127.0.0.1:5092.
الغرض: فحص الواجهة على RC بلا الحاجة إلى تجاوز auth_basic الخاصّ بنجينكس، وبلا تعديل أيّ إعداد
على الخادم. لا يكتب شيئًا: مجرّد وسيط قراءة/تمرير.
"""
import http.server, socketserver, urllib.request, urllib.error, os, sys

DIST = "/tmp/rc-ui/dist"
API = "http://127.0.0.1:5092"
PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 5199


class H(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **k):
        super().__init__(*a, directory=DIST, **k)

    def log_message(self, *a):
        pass

    def _proxy(self, method):
        length = int(self.headers.get("Content-Length") or 0)
        body = self.rfile.read(length) if length else None
        req = urllib.request.Request(API + self.path, data=body, method=method)
        for h in ("Content-Type", "Authorization", "Accept"):
            if self.headers.get(h):
                req.add_header(h, self.headers[h])
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                data, status, hdrs = r.read(), r.status, dict(r.headers)
        except urllib.error.HTTPError as e:
            data, status, hdrs = e.read(), e.code, dict(e.headers)
        except Exception as e:
            data, status, hdrs = str(e).encode(), 502, {}
        self.send_response(status)
        self.send_header("Content-Type", hdrs.get("Content-Type", "application/json"))
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        if self.path.startswith("/api/") or self.path.startswith("/hubs/"):
            return self._proxy("GET")
        # احتياطيّ SPA: أيّ مسار غير ملفّ حقيقيّ يُخدَم بـindex.html
        rel = self.path.split("?")[0].lstrip("/")
        if rel and not os.path.isfile(os.path.join(DIST, rel)):
            self.path = "/index.html"
        return super().do_GET()

    def do_POST(self):
        return self._proxy("POST")

    def do_PUT(self):
        return self._proxy("PUT")

    def do_DELETE(self):
        return self._proxy("DELETE")


socketserver.ThreadingTCPServer.allow_reuse_address = True
with socketserver.ThreadingTCPServer(("127.0.0.1", PORT), H) as httpd:
    print(f"RC UI proxy on http://127.0.0.1:{PORT} (dist={DIST} api={API})", flush=True)
    httpd.serve_forever()
