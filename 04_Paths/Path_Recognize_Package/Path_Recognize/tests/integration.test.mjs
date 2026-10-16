import assert from 'node:assert/strict';
import {recognize,exportNetwork,updateMask} from '../dist/path-recognize.mjs';
import {analyzeMask} from '../dist/engine.mjs';
import {reconstructGaps,annotateEvidence} from '../dist/reconstruction.mjs';
import {acceptNetwork} from '../examples/downstream.mjs';
const width=100,height=65,roi={x:0,y:0,width,height};
const rgba=new Uint8ClampedArray(width*height*4);
for(let y=0;y<height;y++)for(let x=0;x<width;x++){
 const value=y>=27&&y<=35&&x>=8&&x<=91?25:220;rgba.set([value,value,value,255],(y*width+x)*4);
}
const before=rgba.slice();
const input={rgba,width,height,roi,options:{detectSurface:false,reconstruct:false,denoise:0,minArea:0},smoothing:1};
const packet=recognize(input);assert.deepEqual(rgba,before);assert.ok(packet.network.edges.length);
acceptNetwork(packet.network);assert.equal(packet.network.schemaVersion,'2.5');
const scaled=recognize({...input,image:{name:'2x.png',width:width*2,height:height*2}});
assert.equal(scaled.network.edges.length,packet.network.edges.length);
packet.network.edges.forEach((edge,i)=>edge.points.forEach((point,j)=>point.forEach((v,k)=>assert.ok(Math.abs(scaled.network.edges[i].points[j][k]-2*v)<.002))));
assert.ok(Math.abs(scaled.network.stats.lengthPx/packet.network.stats.lengthPx-2)<1e-9);
assert.deepEqual(scaled.network.processing.roi,roi);assert.equal(scaled.network.roi.width,2*width);acceptNetwork(scaled.network);
const previousMask=packet.analysis.mask.slice(),cut=previousMask.slice();for(let y=0;y<height;y++)cut[y*width+47]=0;
const edited=updateMask(packet,cut);assert.deepEqual(packet.analysis.mask,previousMask);assert.equal(packet.context.manualEdits,0);assert.equal(edited.network.processing.manualEdits,1);
assert.equal(edited.network.stats.components,2);for(let y=0;y<height;y++)assert.equal(edited.analysis.mask[y*width+47],0);acceptNetwork(edited.network);
// A real reconstructed faint gap must disappear, including graph connectivity, when excluded.
const gray=new Float32Array(width*height).fill(185),mask=new Uint8Array(width*height);
for(let y=27;y<=35;y++)for(let x=8;x<=91;x++){const gap=x>=40&&x<=53;gray[y*width+x]=gap?105:25;if(!gap)mask[y*width+x]=1;}
const filled=reconstructGaps({gray,mask,width,height,roi,result:analyzeMask(mask,width,height,roi),options:{maxGap:40}});
assert.ok(filled.bridges.length);const analysis={...packet.analysis,...annotateEvidence(analyzeMask(filled.mask,width,height,roi),filled.inferredMask,filled.bridges,width)};delete analysis.centerGraph;
const withInference=exportNetwork(analysis,packet.context,{pathMode:'full'}),direct=exportNetwork(analysis,packet.context,{pathMode:'full',includeInference:false});
assert.equal(withInference.stats.components,1);assert.ok(withInference.quality.includesInferredConnections);assert.equal(direct.stats.components,2);assert.equal(direct.quality.includesInferredConnections,false);assert.deepEqual(direct.inferredConnections,[]);assert.ok(direct.edges.every(e=>e.inferredSegments.every(v=>!v)));acceptNetwork(withInference);acceptNetwork(direct);
const empty=recognize({...input,rgba:new Uint8ClampedArray(rgba.length).fill(255)});assert.equal(empty.network.edges.length,0);acceptNetwork(empty.network);
const transparent=recognize({...input,rgba:new Uint8ClampedArray(rgba.length)});assert.deepEqual(transparent.analysis.mask,empty.analysis.mask);
assert.throws(()=>recognize({...input,width:1401}),/dimensions/);
assert.throws(()=>recognize({...input,rgba:new Uint8Array(3)}),/rgba/);
assert.throws(()=>recognize({...input,options:{fake:true}}),/Unknown/);
assert.throws(()=>recognize({...input,options:{sensitivity:101}}),/Invalid/);
assert.throws(()=>recognize({...input,smoothing:4}),/smoothing/);
assert.throws(()=>updateMask(packet,new Uint8Array(width*height).fill(2)),/binary/);
const exported=exportNetwork(packet.analysis,packet.context,{smoothing:3,pathMode:'full'});assert.equal(packet.context.smoothing,1);acceptNetwork(exported);
assert.deepEqual(JSON.parse(JSON.stringify(exported)),exported,'network contains JSON-safe values only');
console.log('PASS: standalone recognition, downstream consumer, original-coordinate scaling, manual edits, inference exclusion reconnect analysis, transparency, empty inputs, immutable inputs, invalid argument rejection, JSON roundtrip.');
