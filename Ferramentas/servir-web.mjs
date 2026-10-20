// Servidor local para o build WebGL. Execute: node Ferramentas/servir-web.mjs
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../Build/Web');
const types={'.html':'text/html; charset=utf-8','.js':'application/javascript','.wasm':'application/wasm','.data':'application/octet-stream','.json':'application/json','.css':'text/css','.png':'image/png','.txt':'text/plain; charset=utf-8'};
http.createServer((req,res)=>{
 let file;try{file=path.resolve(root,'.'+decodeURIComponent(new URL(req.url,'http://localhost').pathname));}catch{res.writeHead(400).end();return;}
 if(file!==root&&!file.startsWith(root+path.sep)){res.writeHead(403).end();return;}
 if(file===root||file.endsWith(path.sep))file=path.join(file,'index.html');
 fs.stat(file,(error,stat)=>{if(error||!stat.isFile()){res.writeHead(404).end('Arquivo indisponível');return;}
 res.writeHead(200,{'Content-Type':types[path.extname(file)]||'application/octet-stream','Content-Length':stat.size,'Cross-Origin-Opener-Policy':'same-origin','Cross-Origin-Embedder-Policy':'require-corp'});fs.createReadStream(file).pipe(res);});
}).listen(8080,'127.0.0.1',()=>console.log('Dev City Web: http://127.0.0.1:8080'));
