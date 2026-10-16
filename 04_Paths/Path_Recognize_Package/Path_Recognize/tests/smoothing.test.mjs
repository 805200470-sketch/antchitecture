import assert from 'node:assert/strict';
import {smoothGraph,segmentInside} from '../dist/path-smoothing.mjs';
const width=80,height=80,mask=new Uint8Array(width*height).fill(1);
const points=Array.from({length:35},(_,i)=>[10.5+i,15.5+Math.floor(i/2)]);
const loop=[[50.5,50.5],[51.5,50.5],[52.5,51.5],[51.5,52.5],[50.5,51.5],[50.5,50.5]];
const graph={nodes:[{id:1,x:10.5,y:15.5},{id:2,x:44.5,y:32.5},{id:3,x:50.5,y:50.5}],edges:[{id:1,source:1,target:2,points,lengthPx:40},{id:2,source:3,target:3,points:loop,lengthPx:6}]};
const inferredMask=new Uint8Array(width*height);inferredMask[20*width+20]=1;
const result={mask,inferredMask,graph},before=JSON.stringify(graph);
const raw=smoothGraph(result,width,height,0),mild=smoothGraph(result,width,height,1);
assert.deepEqual(raw.edges[0].points,points,'zero strength must be exact');
assert.equal(JSON.stringify(graph),before,'source graph must remain intact');
assert.deepEqual(mild.nodes,graph.nodes);assert.equal(mild.edges.length,graph.edges.length);
assert.ok(mild.smoothing.maxDisplacementPx>0);assert.ok(mild.smoothing.maxDisplacementPx<=1.25+1e-9);
const roughness=ps=>ps.slice(1,-1).reduce((sum,p,j)=>{const a=ps[j],b=ps[j+2];return sum+Math.hypot(b[0]-2*p[0]+a[0],b[1]-2*p[1]+a[1]);},0);
assert.ok(roughness(mild.edges[0].points)<roughness(points)*.5,'pixel stairs should become substantially gentler');
for(let level=1;level<=3;level++){
 const r=smoothGraph(result,width,height,level);
 for(let i=0;i<graph.edges.length;i++){
  const e=r.edges[i],old=graph.edges[i];assert.equal(e.source,old.source);assert.equal(e.target,old.target);assert.equal(e.points.length,old.points.length);
  assert.deepEqual(e.points[0],old.points[0]);assert.deepEqual(e.points.at(-1),old.points.at(-1));assert.deepEqual(e.inferredSegments,raw.edges[i].inferredSegments);
 }
 assert.deepEqual(r.edges[1].points[0],r.edges[1].points.at(-1),'loop stays closed');
}
// One-pixel stairs: any movement must respect the corridor and cannot cut a soil corner.
const thin=new Uint8Array(width*height);for(const [x,y]of points)thin[Math.floor(y)*width+Math.floor(x)]=1;
const constrained=smoothGraph({...result,mask:thin,graph:{...graph,edges:[graph.edges[0]]}},width,height,3);
for(let i=1;i<points.length;i++)assert.ok(segmentInside(constrained.edges[0].points[i-1],constrained.edges[0].points[i],thin,width,height));
// A large turn only changes inside the configured small displacement bound.
const corner=Array.from({length:20},(_,i)=>[10.5+i,30.5]).concat(Array.from({length:19},(_,i)=>[29.5,31.5+i]));
const bend=smoothGraph({...result,graph:{nodes:[],edges:[{points:corner}]}},width,height,1);
assert.ok(bend.smoothing.maxDisplacementPx<=1.25+1e-9);
assert.deepEqual(smoothGraph({mask,graph:{nodes:[],edges:[]}},width,height).edges,[]);
assert.deepEqual(smoothGraph(result,width,height,1),mild,'deterministic');
console.log('PASS: gentler stairs, bounded corners, fixed nodes, closed loops, same topology/provenance, exact off mode, narrow-wall constraints, immutable source.');
