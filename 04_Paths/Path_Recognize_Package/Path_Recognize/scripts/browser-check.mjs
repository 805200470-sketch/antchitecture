/** Optional browser regression suite. Requires Playwright + a local Chrome install. */
import assert from 'node:assert/strict';
import {readFile,mkdir,writeFile} from 'node:fs/promises';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
const {chromium}=await import(process.env.PLAYWRIGHT_MODULE||'playwright');
const root=fileURLToPath(new URL('../',import.meta.url)),port=Number(process.env.PATH_RECOGNIZE_TEST_PORT||8876),url=`http://127.0.0.1:${port}`;
const server=spawn(process.execPath,['scripts/serve.mjs','--port',String(port)],{cwd:root,stdio:'ignore'});
const clean=value=>{value=structuredClone(value);delete value.createdAt;return value;};
const report={date:new Date().toISOString(),checks:[]},errors=[],external=[];
let browser;
const pass=name=>{report.checks.push(name);console.log('PASS:',name);};
async function waitReady(page){await page.waitForFunction(()=>window.Formic?.getResult()&&!window.Formic.getState().busy,null,{timeout:90000});}
async function noChinese(page){
 const unexpected=await page.evaluate(()=>{
  const walker=document.createTreeWalker(document.body,NodeFilter.SHOW_TEXT),found=[];
  while(walker.nextNode()){
   const node=walker.currentNode,element=node.parentElement;
   if(element.closest('script,style,[data-language],#file-name,#toast,pre'))continue;
   if(element.getClientRects().length&&/\p{Script=Han}/u.test(node.textContent))found.push(node.textContent.trim());
  }
  for(const element of document.querySelectorAll('[title],[aria-label]'))if(!element.closest('[data-language],#file-name'))for(const attr of ['title','aria-label'])if(/\p{Script=Han}/u.test(element.getAttribute(attr)||''))found.push(element.getAttribute(attr));
  return found;
 });assert.deepEqual(unexpected,[]);
}
try{
 for(let i=0;i<50;i++){try{if((await fetch(url+'/dist/')).ok)break;}catch{}await new Promise(r=>setTimeout(r,100));}
 browser=await chromium.launch({channel:process.env.CHROME_CHANNEL||'chrome',headless:true});report.browser=browser.version();
 const context=await browser.newContext({viewport:{width:1440,height:1000},acceptDownloads:true});
 context.on('page',p=>{p.on('pageerror',e=>errors.push(e.message));p.on('request',r=>{if(!r.url().startsWith(url)&&!r.url().startsWith('blob:')&&!r.url().startsWith('data:'))external.push(r.url());});});
 const page=await context.newPage();await page.goto(url+'/dist/');await waitReady(page);
 assert.equal(await page.locator('html').getAttribute('lang'),'zh-CN');
 const color=await page.evaluate(()=>window.Formic.getResult());assert.deepEqual(clean(color),clean(JSON.parse(await readFile(root+'examples/results/color-network.json','utf8'))));pass('Chinese default; color-image network exactly matches the previous package');
 await page.locator('[data-language=en]').click();assert.deepEqual(clean(await page.evaluate(()=>window.Formic.getResult())),clean(color));
 for(const step of ['region','extract','correct','export']){await page.locator(`[data-step=${step}]`).click();if(step==='extract')await page.locator('.advanced').evaluate(el=>el.open=true);await noChinese(page);}
 await page.locator('#help-button').click();await noChinese(page);await page.locator('#close-help').click();
 await page.locator('[data-step=correct]').click();
 for(const view of ['original','enhanced','mask','overlay','skeleton','surface','depth','terrain']){await page.locator(`[data-view=${view}]`).click();await noChinese(page);}
 await page.locator('[data-view=overlay]').click();
 for(const level of ['0','1','2','3']){await page.locator('#path-smoothing').fill(level);await noChinese(page);}await page.locator('#path-smoothing').fill('1');
 pass('English covers all workflow panels, eight layers, smoothing levels, guide and accessibility labels');
 await page.reload();await waitReady(page);assert.equal(await page.locator('html').getAttribute('lang'),'en');pass('Language preference survives reload');
 await page.evaluate(()=>window.Formic.loadFile(new File(['x'],'invalid.txt',{type:'text/plain'})));assert.match(await page.locator('#toast').innerText(),/Choose a JPG/);pass('Invalid upload displays a translated error');
 await page.locator('#file-input').setInputFiles(root+'examples/assets/gray-nest.png');await page.waitForFunction(()=>document.getElementById('file-name').textContent==='gray-nest.png'&&window.Formic.getState().step==='region');assert.equal(await page.evaluate(()=>window.Formic.getResult()),null);
 await page.locator('#continue-button').click();assert.equal(await page.evaluate(()=>window.Formic.getState().step),'extract');await page.locator('#extract-button').click();await waitReady(page);
 const gray=await page.evaluate(()=>window.Formic.getResult());assert.deepEqual(clean(gray),clean(JSON.parse(await readFile(root+'examples/results/gray-network.json','utf8'))));pass('Image replacement, region → extraction workflow and grayscale network match the previous package');
 // Real manual edit followed by a language switch must retain the result and history.
 await page.locator('#erase-tool').click();let box=await page.locator('#interaction-canvas').boundingBox();await page.mouse.move(box.x+box.width*.37,box.y+box.height*.60);await page.mouse.down();await page.mouse.move(box.x+box.width*.37,box.y+box.height*.70,{steps:6});await page.mouse.up();
 await page.waitForFunction(()=>window.Formic.getState().corrections===1&&!window.Formic.getState().busy,null,{timeout:90000});
 const edited=await page.evaluate(()=>({network:window.Formic.getResult(),state:window.Formic.getState()}));await page.locator('[data-language=zh]').click();
 const switched=await page.evaluate(()=>({network:window.Formic.getResult(),state:window.Formic.getState()}));assert.deepEqual(clean(edited.network),clean(switched.network));delete edited.state.language;delete switched.state.language;assert.deepEqual(edited.state,switched.state);
 await page.locator('#undo-button').click();await page.waitForFunction(()=>!window.Formic.getState().busy&&window.Formic.getState().corrections===0);assert.deepEqual(clean(await page.evaluate(()=>window.Formic.getResult())),clean(gray));
 await page.locator('#redo-button').click();await page.waitForFunction(()=>!window.Formic.getState().busy&&window.Formic.getState().corrections===1);pass('Switching languages preserves edits, ROI, graph, view and undo / redo history');
 await page.locator('#undo-button').click();await page.waitForFunction(()=>!window.Formic.getState().busy&&window.Formic.getState().corrections===0);
 assert.equal(await page.locator('#show-mask').isChecked(),false);assert.equal(await page.locator('#show-nodes').isChecked(),false);
 await page.locator('#export-button').click();
 for(const kind of ['json','svg','mask','centerline','overlay','depth']){
  const event=page.waitForEvent('download');await page.locator(`[data-export=${kind}]`).click();const download=await event;const bytes=await readFile(await download.path());assert.ok(bytes.length>30);
  if(kind==='json')assert.deepEqual(clean(JSON.parse(bytes.toString())),clean(gray));
  if(kind==='svg')assert.ok(bytes.toString().includes('<svg'));
  if(['mask','centerline','overlay','depth'].includes(kind))assert.equal(bytes.subarray(1,4).toString(),'PNG');
  if(kind==='centerline'){
   const alpha=await page.evaluate(async data=>{const blob=await(await fetch('data:image/png;base64,'+data)).blob(),image=await createImageBitmap(blob),c=document.createElement('canvas');c.width=image.width;c.height=image.height;const x=c.getContext('2d');x.drawImage(image,0,0);const pixels=x.getImageData(0,0,c.width,c.height).data;let transparent=0,drawn=0;for(let i=3;i<pixels.length;i+=4)pixels[i]?drawn++:transparent++;return {transparent,drawn};},bytes.toString('base64'));assert.ok(alpha.drawn>0&&alpha.transparent>alpha.drawn);
  }
 }
 assert.equal((await page.evaluate(()=>window.Formic.getResult(false))).quality.includesInferredConnections,false);await page.locator('#continue-button').click();assert.equal(await page.evaluate(()=>window.Formic.getState().step),'correct');pass('Six export formats, transparent centerlines, inference exclusion and return to refinement');
 await mkdir(root+'qa-artifacts',{recursive:true});await page.evaluate(()=>document.getElementById('toast').hidden=true);await page.screenshot({path:root+'qa-artifacts/workspace-zh.png',fullPage:true});await page.locator('[data-language=en]').click();await page.screenshot({path:root+'qa-artifacts/workspace-en.png',fullPage:true});
 await page.locator('[data-step=extract]').click();await page.locator('#sensitivity').fill('51');assert.ok(await page.evaluate(()=>window.Formic.getState().dirty));await noChinese(page);await page.locator('[data-language=zh]').click();assert.ok(await page.evaluate(()=>window.Formic.getState().dirty));await page.locator('#reset-params').click();
 await page.evaluate(()=>{window.pendingRecognition=window.Formic.extract();});await page.locator('[data-language=en]').click();await page.locator('#cancel-button').click();await page.evaluate(()=>window.pendingRecognition);assert.equal(await page.evaluate(()=>window.Formic.getState().busy),false);pass('Dirty settings and language changes during processing; cancellation retains completed results');
 for(const language of ['zh','en']){
  await page.evaluate(language=>window.Formic.setLanguage(language),language);
  for(const width of [390,768,1440]){await page.setViewportSize({width,height:900});assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>innerWidth),false);if(language==='en')await noChinese(page);}
 }
 await page.setViewportSize({width:390,height:844});await page.locator('[data-step=correct]').click();await page.evaluate(()=>document.getElementById('toast').hidden=true);await page.screenshot({path:root+'qa-artifacts/workspace-mobile.png',fullPage:true});pass('Chinese and English layouts fit mobile, tablet and desktop without horizontal page overflow');
 const demo=await context.newPage();await demo.goto(url+'/examples/');await noChinese(demo);await demo.locator('#gray').click();await demo.waitForFunction(()=>!window.integrationDemo.busy&&window.integrationDemo.packet,null,{timeout:90000});await noChinese(demo);const demoNetwork=await demo.evaluate(()=>window.integrationDemo.packet.network);await demo.locator('[data-language=zh]').click();assert.deepEqual(clean(await demo.evaluate(()=>window.integrationDemo.packet.network)),clean(demoNetwork));await demo.screenshot({path:root+'qa-artifacts/integration-zh.png',fullPage:true});await demo.locator('#inference').uncheck();await demo.waitForFunction(()=>!window.integrationDemo.busy);assert.equal(await demo.evaluate(()=>window.integrationDemo.packet.network.quality.includesInferredConnections),false);pass('Bilingual integration demo, downstream consumer and inference-free worker export');
 const restricted=await browser.newContext();await restricted.addInitScript(()=>Object.defineProperty(window,'localStorage',{get(){throw new DOMException('Storage denied','SecurityError');}}));const restrictedPage=await restricted.newPage();await restrictedPage.goto(url+'/dist/');await waitReady(restrictedPage);assert.equal(await restrictedPage.locator('html').getAttribute('lang'),'zh-CN');await restrictedPage.locator('[data-language=en]').click();assert.equal(await restrictedPage.locator('html').getAttribute('lang'),'en');await restricted.close();pass('Language switching works when browser storage is unavailable');
 assert.deepEqual(errors,[]);assert.deepEqual(external,[]);pass('No uncaught browser errors or external runtime requests');report.success=true;await writeFile(root+'qa-artifacts/browser-checks.json',JSON.stringify(report,null,2)+'\n');
}finally{await browser?.close();server.kill();}
