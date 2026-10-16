"""Standard-library fallback for opening the page without Node/npm."""
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from functools import partial
import sys, threading, webbrowser
root=Path(__file__).resolve().parent.parent
port=int(sys.argv[1]) if len(sys.argv)>1 else 8765
class Handler(SimpleHTTPRequestHandler):
    extensions_map={**SimpleHTTPRequestHandler.extensions_map,'.mjs':'text/javascript','.js':'text/javascript'}
server=ThreadingHTTPServer(('127.0.0.1',port),partial(Handler,directory=str(root)))
url=f'http://127.0.0.1:{port}/dist/'
print(f'Path_Recognize: {url}\nCtrl+C to stop.',flush=True)
threading.Timer(.3,lambda:webbrowser.open(url)).start()
try: server.serve_forever()
except KeyboardInterrupt: pass
finally: server.server_close()
