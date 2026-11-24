import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const dist=path.join(root,'sites/file-renamer-dohi/dist');
const out=path.join(root,'output/store-submission/1.0.2.0');
const errors=[];
const pages=[['site',path.join(dist,'index.html')],['site',path.join(dist,'privacy/index.html')],['site',path.join(dist,'support/index.html')],['local',path.join(out,'처음부터-따라하기.html')],['local',path.join(out,'개인정보-안내.html')],['local',path.join(out,'지원-안내.html')]];
for(const [kind,file] of pages){
  const html=fs.readFileSync(file,'utf8');
  if(!html.includes('lang="ko"')||!html.includes('charset="utf-8"')) errors.push('locale: '+file);
  if(html.includes('\ufffd')) errors.push('encoding: '+file);
  const ids=new Set([...html.matchAll(/\bid="([^"]+)"/g)].map(m=>m[1]));
  for(const [,href] of html.matchAll(/(?:href|src)="([^"]+)"/g)){
    if(/^(https?:|mailto:|data:)/.test(href))continue;
    if(href.startsWith('#')){if(!ids.has(href.slice(1)))errors.push('anchor: '+href);continue;}
    let target=href.startsWith('/')?path.join(dist,href):path.resolve(path.dirname(file),href);
    if(!fs.existsSync(target)){errors.push('missing: '+target);continue;}
    if(fs.statSync(target).isDirectory()&&!fs.existsSync(path.join(target,'index.html'))) errors.push('missing index: '+target);
  }
  if(kind==='site'&&(/<script/i.test(html)||/token|BEGIN PRIVATE KEY|CN=E48A/i.test(html)))errors.push('unintended public content: '+file);
}
const pngs=fs.readdirSync(path.join(out,'screenshots')).filter(n=>n.endsWith('.png'));
if(pngs.length!==4) errors.push('expected 4 PNG screenshots');
const images=pngs.map(n=>{const b=fs.readFileSync(path.join(out,'screenshots',n)); const width=b.readUInt32BE(16),height=b.readUInt32BE(20);if(width<1366||height<768||b.length>50*1024*1024)errors.push('image spec: '+n);return {name:n,width,height,bytes:b.length};});
console.log(JSON.stringify({pages:pages.length,images,errors},null,2));
if(errors.length)process.exitCode=1;
