import assert from 'node:assert/strict';
import {analyzeMask,extractPaths} from '../dist/engine.mjs';
import {reconstructGaps,annotateEvidence} from '../dist/reconstruction.mjs';
const width=100,height=65,roi={x:0,y:0,width,height};
function fixture(gapBrightness=105){const gray=new Float32Array(width*height).fill(185),mask=new Uint8Array(width*height);for(let y=27;y<=35;y++)for(let x=8;x<=91;x++){const gap=x>=40&&x<=53;gray[y*width+x]=gap?gapBrightness:25;if(!gap)mask[y*width+x]=1;}return {gray,mask};}
const supported=fixture(),base=analyzeMask(supported.mask,width,height,roi);assert.equal(base.stats.components,2);
let r=reconstructGaps({...supported,width,height,roi,result:base,options:{maxGap:40}});assert.ok(r.bridges.length>=1,'a lighter but coherent channel should bridge');assert.equal(analyzeMask(r.mask,width,height,roi).stats.components,1);assert.ok(r.inferredMask.some(Boolean));assert.ok(r.bridges.every(b=>b.evidence.stabilityDeviationPx<=3));
const blocked=fixture(185);let no=reconstructGaps({...blocked,width,height,roi,result:analyzeMask(blocked.mask,width,height,roi),options:{maxGap:40}});assert.equal(no.bridges.length,0,'bright solid soil must not be bridged');
no=reconstructGaps({...supported,width,height,roi,result:base,options:{maxGap:6}});assert.equal(no.bridges.length,0,'maximum gap is a real bound');
no=reconstructGaps({...supported,width,height,roi,result:base,options:{reconstruct:false}});assert.equal(no.bridges.length,0);assert.deepEqual(no.mask,supported.mask);
const edited=r.mask.slice();for(let y=0;y<height;y++)edited[y*width+47]=0;const post=annotateEvidence(analyzeMask(edited,width,height,roi),r.inferredMask,r.bridges,width);assert.equal(post.stats.components,2,'erasing a bridge stays disconnected');for(let y=0;y<height;y++)assert.equal(post.inferredMask[y*width+47],0);
const rgba=new Uint8ClampedArray(width*height*4);for(let i=0;i<supported.gray.length;i++)rgba.set([supported.gray[i],supported.gray[i],supported.gray[i],255],i*4);
const opts={rgba,width,height,roi,options:{reconstruct:true,maxGap:40}};const a=extractPaths(opts),b=extractPaths(opts);assert.deepEqual(a.mask,b.mask);assert.deepEqual(a.bridges,b.bridges);assert.ok(a.depth.every(v=>v>=0&&v<=1));
console.log('PASS: supported faint channel bridges; solid barrier rejected; maximum distance enforced; inference disabled; manual cuts preserved; deterministic relief and provenance.');
