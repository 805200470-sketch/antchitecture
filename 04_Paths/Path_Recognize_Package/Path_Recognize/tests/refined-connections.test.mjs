import assert from 'node:assert/strict';
import {analyzeMask} from '../dist/engine.mjs';
import {terrainEvidence,reconstructGaps,annotateEvidence} from '../dist/reconstruction.mjs';
import {surfaceEvidence,fuseSurfaceEvidence} from '../dist/surface-evidence.mjs';
import {centerRoutes} from '../dist/center-routes.mjs';
const w=110,h=70,roi={x:0,y:0,width:w,height:h};
function fixture(wall=false){
 const gray=new Float32Array(w*h).fill(220),mask=new Uint8Array(w*h);
 for(let y=28;y<=42;y++)for(let x=8;x<=101;x++){const gap=x>=47&&x<=51;gray[y*w+x]=gap?(wall?220:140):50;if(!gap)mask[y*w+x]=1;}
 const n=(id,x)=>({id,x:x+.5,y:35.5,type:'endpoint',boundary:false,clusterPixels:[[x+.5,35.5]]});
 const nodes=[n('a',14),n('b',39),n('c',59),n('d',96)],edges=[];
 for(const [a,b]of [[nodes[0],nodes[1]],[nodes[2],nodes[3]]])edges.push({id:a.id+b.id,source:a.id,target:b.id,lengthPx:b.x-a.x,meanWidthPx:15,points:Array.from({length:b.x-a.x+1},(_,i)=>[a.x+i,35.5])});
 const field=fuseSurfaceEvidence(terrainEvidence(gray,w,h,roi),surfaceEvidence(gray,w,h,roi));
 return {gray,mask,field,result:{mask,graph:{nodes,edges}}};
}
const f=fixture(),args={width:w,height:h,roi,...f},before=f.mask.slice();
const legacy=reconstructGaps({...args,options:{maxGap:6}});assert.equal(legacy.bridges.length,0,'interior endpoint separation exceeds the old radius');
const r=reconstructGaps({...args,options:{maxGap:6,centerlinePass:true}});assert.ok(r.bridges.length,'a six-pixel missing length can connect centerlines set back inside the mask');
assert.ok(r.bridges.every(b=>b.stage==='centerline-refinement'&&b.evidence.gapLengthPx<=6));
assert.equal(analyzeMask(r.mask,w,h,roi).stats.components,1);assert.deepEqual(f.mask,before);
const tooShort=reconstructGaps({...args,options:{maxGap:4,centerlinePass:true}});assert.equal(tooShort.bridges.length,0,'bound the missing route length, not padded search distance');
const wall=fixture(true);assert.equal(reconstructGaps({width:w,height:h,roi,...wall,options:{maxGap:30,centerlinePass:true}}).bridges.length,0,'even a five-pixel bright wall is not a faint channel');
assert.equal(reconstructGaps({width:w,height:h,roi,...wall,options:{maxGap:30}}).bridges.length,0,'raw skeleton bridging also respects the bright wall');
const disabled=reconstructGaps({...args,options:{reconstruct:false,centerlinePass:true}});assert.deepEqual(disabled.mask,f.mask);
const cut=r.mask.slice();for(let y=0;y<h;y++)cut[y*w+49]=0;
const edited=annotateEvidence(analyzeMask(cut,w,h,roi),r.inferredMask,r.bridges,w),g=centerRoutes(edited,w,h);
for(const e of g.edges)assert.ok(e.points.every(([x])=>x<49)||e.points.every(([x])=>x>=50),'manual cuts remain unconnected after centerline pruning');
assert.deepEqual(reconstructGaps({...args,options:{maxGap:6,centerlinePass:true}}).mask,r.mask);
console.log('PASS: recessed centerline ends connect across a bounded image-supported gap, five-pixel wall rejected, max missing length enforced, provenance retained, manual cuts preserved, deterministic.');
