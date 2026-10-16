import assert from 'node:assert/strict';
import {surfaceEvidence,growSurface} from '../dist/surface-evidence.mjs';
import {extractPaths,segment,analyzeMask} from '../dist/engine.mjs';
const w=180,h=96,roi={x:0,y:0,width:w,height:h};
function fixture(barrier=false){
 const gray=new Float32Array(w*h),seed=new Uint8Array(w*h);
 for(let y=0;y<h;y++)for(let x=0;x<w;x++){
  // Deterministic grain over a low-contrast shallow channel with dark ends.
  const texture=((x*97+y*31+x*y*7)%23)-11;
  const inChannel=y>=40&&y<=55&&x>=12&&x<=167;
  const wall=barrier&&x>=84&&x<=97;
  gray[y*w+x]=(inChannel&&!wall?(x<70||x>110?65:202):235)+texture;
  if(inChannel&&(x<70||x>110))seed[y*w+x]=1;
 }
 return {gray,seed};
}
const f=fixture(),prior=f.seed.slice(),field=surfaceEvidence(f.gray,w,h,roi),grown=growSurface(f.seed,field,w,h,roi);
assert.equal(analyzeMask(f.seed,w,h,roi).stats.components,2);
assert.equal(analyzeMask(grown.mask,w,h,roi).stats.components,1,'paired sides recover the shallow textured corridor');
assert.ok(grown.mask[48*w+90]);assert.deepEqual(f.seed,prior,'the original seed remains unchanged');
assert.ok(field.paired[48*w+90]>.46);
const solid=fixture(true),blocked=growSurface(solid.seed,surfaceEvidence(solid.gray,w,h,roi),w,h,roi);
assert.equal(analyzeMask(blocked.mask,w,h,roi).stats.components,2,'solid surface between tunnels remains a barrier');
assert.equal(blocked.mask[48*w+90],0);
const step=new Float32Array(w*h);for(let y=0;y<h;y++)for(let x=0;x<w;x++)step[y*w+x]=x<90?90:220;
const stepField=surfaceEvidence(step,w,h,roi);assert.ok(stepField.paired[48*w+88]<.05,'a single shadow edge lacks a pair of brighter sides');
const smallROI={x:15,y:30,width:145,height:35},cropped=f.seed.map((v,p)=>p%w<15||p%w>=160||Math.floor(p/w)<30||Math.floor(p/w)>=65?0:v);
const bounded=growSurface(cropped,surfaceEvidence(f.gray,w,h,smallROI),w,h,smallROI);
for(let p=0;p<bounded.mask.length;p++)if(bounded.mask[p])assert.ok(p%w>=15&&p%w<160&&Math.floor(p/w)>=30&&Math.floor(p/w)<65);
const rgba=new Uint8ClampedArray(w*h*4);for(let p=0;p<f.gray.length;p++)rgba.set([f.gray[p],f.gray[p],f.gray[p],255],p*4);
const options={detectSurface:false,reconstruct:false,minArea:0};
const off=extractPaths({rgba,width:w,height:h,roi,options});assert.deepEqual(off.mask,segment(rgba,w,h,roi,options).mask);assert.equal(off.surfaceDiagnostics.enabled,false);
const on=extractPaths({rgba,width:w,height:h,roi,options:{reconstruct:false}}),repeat=extractPaths({rgba,width:w,height:h,roi,options:{reconstruct:false}});
assert.deepEqual(on.mask,repeat.mask);assert.deepEqual(on.surfaceCue,repeat.surfaceCue);assert.ok(on.surfaceCue.every(v=>v>=0&&v<=1));
// Re-analysis used by brush/history never invokes automatic surface growth.
const cut=grown.mask.slice();for(let y=0;y<h;y++)for(let x=86;x<=94;x++)cut[y*w+x]=0;
assert.equal(analyzeMask(cut,w,h,roi).stats.components,2);
console.log('PASS: shallow textured groove recovered, bright wall and one-sided shadow rejected, ROI preserved, immutable seeds, feature off restores segmentation, deterministic scores, manual cuts remain cuts.');
