"""LAN Unity WebGL preview; survives editor domain reloads."""
import argparse
import ctypes
import http.server
import json
import mimetypes
import os
from pathlib import Path
import threading
import time
from urllib.parse import unquote, urlsplit


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--root', required=True)
    parser.add_argument('--state', required=True)
    parser.add_argument('--port', type=int, default=8788)
    parser.add_argument('--host', default='0.0.0.0')
    parser.add_argument('--parent', type=int, required=True)
    args = parser.parse_args()
    root = Path(args.root).resolve()
    state_path = Path(args.state)

    class Handler(http.server.SimpleHTTPRequestHandler):
        def __init__(self, *a, **kw):
            super().__init__(*a, directory=str(root), **kw)

        def log_message(self, *a):
            pass

        def end_headers(self):
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
            self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
            super().end_headers()

        def send_head(self):
            path = unquote(urlsplit(self.path).path)
            try:
                state = json.loads(state_path.read_text(encoding='utf-8-sig'))
            except (OSError, ValueError):
                state = {'state': 'waiting', 'version': ''}
            if path == '/__preview/status':
                state.update(service='XiuXianBrowserPreview', root=str(root), pid=os.getpid(), host=args.host)
                return self.text_response(json.dumps(state), 'application/json; charset=utf-8')
            if state['state'] != 'ready':
                text = '正在更新 WebGL 游戏，完成后自动载入…' if state['state'] == 'building' else '尚未完成构建，请在 Unity 中更新浏览器版。'
                return self.text_response('<!doctype html><meta charset=utf-8><meta http-equiv=refresh content=3><title>仙途浏览器预览</title><p>'+text+'</p>', 'text/html; charset=utf-8', 503)
            target = (root / path.lstrip('/').replace('\\', '/')).resolve()
            if not target.is_relative_to(root):
                self.send_error(403)
                return None
            if target.is_dir():
                target = target / 'index.html'
            if not target.is_file():
                self.send_error(404)
                return None
            try:
                stream = target.open('rb')
            except OSError:
                self.send_error(404)
                return None
            self.send_response(200)
            inner = target
            if target.suffix in ('.gz', '.br'):
                self.send_header('Content-Encoding', 'gzip' if target.suffix == '.gz' else 'br')
                inner = target.with_suffix('')
            kind = {'.wasm': 'application/wasm', '.js': 'application/javascript', '.data': 'application/octet-stream'}.get(inner.suffix)
            self.send_header('Content-Type', kind or mimetypes.guess_type(str(inner))[0] or 'application/octet-stream')
            self.send_header('Content-Length', str(os.fstat(stream.fileno()).st_size))
            self.end_headers()
            return stream

        def text_response(self, text, kind, code=200):
            import io
            data = text.encode('utf-8')
            self.send_response(code)
            self.send_header('Content-Type', kind)
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            return io.BytesIO(data)

        def do_POST(self):
            self.send_error(405)

    server = http.server.ThreadingHTTPServer((args.host, args.port), Handler)
    server.daemon_threads = True

    def watch_editor():
        kernel = ctypes.WinDLL('kernel32', use_last_error=True)
        kernel.OpenProcess.restype = ctypes.c_void_p
        kernel.OpenProcess.argtypes = [ctypes.c_ulong, ctypes.c_int, ctypes.c_ulong]
        kernel.WaitForSingleObject.argtypes = [ctypes.c_void_p, ctypes.c_ulong]
        kernel.CloseHandle.argtypes = [ctypes.c_void_p]
        handle = kernel.OpenProcess(0x00100000, False, args.parent)
        if not handle:
            server.shutdown()
            return
        try:
            while kernel.WaitForSingleObject(handle, 2000) == 258:
                pass
        finally:
            kernel.CloseHandle(handle)
            server.shutdown()

    threading.Thread(target=watch_editor, daemon=True).start()
    try:
        server.serve_forever()
    finally:
        server.server_close()


if __name__ == '__main__':
    main()
