from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from json import dumps, loads
from threading import Lock
from time import time

messages = []
messages_lock = Lock()


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        if self.path == "/health":
            self.respond(200, {"status": "ok"})
            return
        if self.path == "/messages":
            with messages_lock:
                self.respond(200, {"messages": list(messages)})
            return
        self.respond(404, {"error": "not_found"})

    def do_POST(self):
        length = int(self.headers.get("Content-Length", "0"))
        raw = self.rfile.read(length).decode("utf-8")
        try:
            body = loads(raw) if raw else None
        except ValueError:
            self.respond(400, {"error": "invalid_json"})
            return
        record = {
            "path": self.path,
            "body": body,
            "messageId": self.headers.get("WMS-Message-Id"),
            "signature": self.headers.get("WMS-Signature"),
            "timestamp": self.headers.get("WMS-Timestamp"),
            "receivedAt": time(),
        }
        with messages_lock:
            messages.append(record)
        self.respond(202, {"accepted": True})

    def respond(self, status, payload):
        body = dumps(payload).encode("utf-8")
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def log_message(self, format, *args):
        return


ThreadingHTTPServer(("0.0.0.0", 8080), Handler).serve_forever()
