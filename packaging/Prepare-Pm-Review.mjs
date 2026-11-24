import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const out=path.join(root,'output/pm-review/2026-11-24');
const wack='artifacts/msix/Wack/20261113-003056-f17ce17b';
const evidence=[
  [wack+'/summary.json','wack-summary.json'],
  [wack+'/report.xml','wack-report.xml'],
  [wack+'/diagnostics/post_process_trace_DPIAwarenessValidation_11132026_03316.txt','dpi-analysis-trace.txt'],
  [wack+'/diagnostics/post_process_trace_DetectBlockedExes_11132026_03316.txt','blocked-executables-trace.txt'],
  ['artifacts/msix/Test-1.0.2.0/startup-dpi.json','startup-dpi.json'],
  ['artifacts/msix/Test-1.0.2.0/verification.json','verification.json']
];
fs.mkdirSync(path.join(out,'evidence'),{recursive:true});
for(const [src,dst] of evidence)fs.copyFileSync(path.join(root,src),path.join(out,'evidence',dst));
const escape=s=>s.replaceAll('&','&amp;').replaceAll('<','&lt;').replaceAll('>','&gt;').replaceAll('"','&quot;');
const lines=fs.readFileSync(path.join(out,'PM-검토요청-보고서.txt'),'utf8').split(/\r?\n/);
const content=lines.slice(2).filter(Boolean).map(line=>{
  if(/^\d+\. /.test(line))return `<h2>${escape(line)}</h2>`;
  if(/^https:\/\//.test(line))return `<p class="source"><a href="${escape(line)}">${escape(line)}</a></p>`;
  if(/^(发生现象|발생 현상|확인된 증거|확인된 증거와 한계|아직 확인되지 않은 것|요청 작업|완료 기준|진행 순서|제출 보류 조건|조건부 제출 검토 조건)$/.test(line))return `<h3>${escape(line)}</h3>`;
  return `<p${line.startsWith('우선순위')?' class="label"':''}>${escape(line)}</p>`;
}).join('\n');
const html=`<!doctype html><html lang="ko"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>File Renamer — PM 검토 요청 보고서</title><style>
*{box-sizing:border-box}body{margin:0;background:#eef2f5;color:#1f2937;font:16px/1.8 'Segoe UI','Malgun Gothic',sans-serif;word-break:keep-all;overflow-wrap:anywhere}main{max-width:980px;margin:36px auto;padding:48px 56px;background:white;border:1px solid #d6dde4}h1{font-size:32px;line-height:1.4;margin:10px 0 14px}h2{margin:40px 0 16px;padding-top:20px;border-top:2px solid #284b63;font-size:23px}h3{font-size:17px;margin:24px 0 10px}p{margin:10px 0}.meta{color:#596675;font-size:14px}.notice{background:#fff4df;border-left:4px solid #b7791f;padding:16px 20px;margin:24px 0}.label{font-weight:700;color:#365469}a{color:#175a8b;text-underline-offset:3px}.source{font-size:13px;word-break:break-all}.actions{display:flex;gap:18px;align-items:center;flex-wrap:wrap}button{border:0;background:#284b63;color:white;padding:10px 18px;border-radius:5px;font:inherit;cursor:pointer}footer{margin-top:40px;border-top:1px solid #d6dde4;padding-top:20px;font-size:13px;color:#596675}@media(max-width:650px){main{margin:0;padding:28px 20px;border:0}h1{font-size:26px}}@media print{body{background:white;font-size:10.5pt;line-height:1.65}main{max-width:none;margin:0;padding:0;border:0}.actions{display:none}h1{font-size:22pt}h2{font-size:16pt;break-after:avoid}h3{break-after:avoid}p{orphans:3;widows:3}.notice{break-inside:avoid}a{color:inherit}@page{size:A4;margin:18mm}}
</style></head><body><main><div class="meta">PM 검토용 · 작성 2026-11-24 · 대상 1.0.2.0 x64</div><h1>File Renamer<br>Store 제출 전 검사 이슈 조치 요청</h1><div class="actions"><button onclick="window.print()">인쇄 / PDF로 저장</button><a href="PM-검토요청-보고서.txt">보고서 텍스트</a><a href="PM-전달문.txt">복사용 전달문</a></div><div class="notice"><b>권고 판단:</b> DPI 경고의 추가 조사와 실사용 검증 후 최종 제출 여부를 결정합니다. 현재 판정은 WARNING이며, Store 심사 탈락 결과가 아닙니다.</div>${content}<footer><b>첨부 자료</b><ul>${evidence.map(([,name])=>`<li><a href="evidence/${name}">${name}</a></li>`).join('')}</ul>이 보고서는 기존 기록에 대한 검토·조치 제안입니다. 신규 수정 및 재검사 완료 보고가 아닙니다.</footer></main></body></html>`;
fs.writeFileSync(path.join(out,'PM-검토요청-보고서.html'),html,'utf8');
const linked=[...html.matchAll(/href="([^"]+)"/g)].map(m=>m[1]).filter(s=>!s.startsWith('https://'));
for(const link of linked)if(!fs.existsSync(path.join(out,link)))throw Error('Missing link '+link);
if(html.includes('\ufffd'))throw Error('Encoding issue');
console.log(JSON.stringify({report:out,attachments:evidence.length,localLinksChecked:linked.length}));
