import http from 'node:http';
import {readFile,stat} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {execFile} from 'node:child_process';
const root=fileURLToPath(new URL('../',import.meta.url)),args=process.argv.slice(2),at=args.indexOf('--port'),port=at<0?8765:Number(args[at+1]);
if(!Number.isInteger(port)||port<1||port>65535)throw new Error('Invalid --port');
const mime={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.js':'text/javascript; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.json':'application/json; charset=utf-8','.jpg':'image/jpeg','.png':'image/png','.svg':'image/svg+xml','.md':'text/plain; charset=utf-8'};
const server=http.createServer(async(req,res)=>{
 try{
  if(!['GET','HEAD'].includes(req.method)){res.writeHead(405);res.end();return;}
  let pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
  if(pathname==='/'){res.writeHead(302,{Location:'/dist/'});res.end();return;}
  let file=path.resolve(root,'.'+pathname),relative=path.relative(root,file);
  if(relative.startsWith('..')||path.isAbsolute(relative)){res.writeHead(403);res.end();return;}
  if((await stat(file)).isDirectory())file=path.join(file,'index.html');
  const data=await readFile(file);res.writeHead(200,{'Content-Type':mime[path.extname(file)]??'application/octet-stream','Cache-Control':'no-store','Content-Length':data.length});res.end(req.method==='HEAD'?undefined:data);
 }catch{res.writeHead(404);res.end('Not found');}
});
server.on('error',e=>{console.error(e.code==='EADDRINUSE'?`Port ${port} is busy. Try: node scripts/serve.mjs --port ${port+1} --open`:e.message);process.exitCode=1;});
server.listen(port,'127.0.0.1',()=>{
 const url=`http://127.0.0.1:${port}/dist/`;console.log(`Path_Recognize: ${url}\nIntegration example: http://127.0.0.1:${port}/examples/\nCtrl+C to stop.`);
 if(args.includes('--open')){const command=process.platform==='darwin'?'open':process.platform==='win32'?'explorer.exe':'xdg-open';execFile(command,[url],e=>{if(e)console.log('Open the URL above in your browser.');});}
});
