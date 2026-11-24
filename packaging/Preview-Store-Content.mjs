import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const site=path.join(root,'sites/file-renamer-dohi/dist');
const guide=path.join(root,'output/store-submission/1.0.2.0');
const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.png':'image/png','.txt':'text/plain; charset=utf-8'};
http.createServer((req,res)=>{
  try{
    const pathname=decodeURIComponent(new URL(req.url,'http://localhost').pathname);
    const isGuide=pathname.startsWith('/guide/');
    const base=isGuide?guide:site;
    const relative=isGuide?pathname.slice(7):pathname.slice(1);
    let p=path.resolve(base,relative||'index.html');
    if(!p.startsWith(base+path.sep)&&p!==base) throw Error('outside preview');
    if(fs.statSync(p).isDirectory())p=path.join(p,'index.html');
    res.writeHead(200,{'Content-Type':types[path.extname(p)]||'application/octet-stream'});
    fs.createReadStream(p).pipe(res);
  }catch{res.writeHead(404);res.end('Not found');}
}).listen(4175,'127.0.0.1',()=>console.log('Local: http://127.0.0.1:4175/'));
