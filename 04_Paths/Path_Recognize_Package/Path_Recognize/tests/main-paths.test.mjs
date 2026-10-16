import assert from 'node:assert/strict';
import {selectMainPaths,graphStats} from '../dist/main-paths.mjs';
const node=(id,x,y,boundary=false)=>({id,x,y,boundary,type:'endpoint',clusterPixels:[[x,y]]});
const edge=(id,a,b,width=8,points=[[a.x,a.y],[b.x,b.y]])=>({id,source:a.id,target:b.id,points,meanWidthPx:width,lengthPx:points.slice(1).reduce((s,p,i)=>s+Math.hypot(p[0]-points[i][0],p[1]-points[i][1]),0)});
const a=node('a',20,80),b=node('b',80,80),c=node('c',140,80),d=node('d',80,20),tip=node('tip',80,88);
const g={nodes:[a,b,c,d,tip],edges:[edge('ab',a,b),edge('bc',b,c),edge('bd',b,d),edge('bt',b,tip)]};const before=JSON.stringify(g);
let r=selectMainPaths({graph:g},500,334);assert.equal(r.selection.removedSpurs,1);assert.equal(r.edges.length,3);assert.equal(graphStats(r).junctions,1);assert.ok(!r.nodes.some(n=>n.id==='tip'));assert.equal(JSON.stringify(g),before);
assert.deepEqual(selectMainPaths({graph:g},500,334,'full').edges,g.edges);
// A genuine short entrance at the image/ROI boundary must survive pruning.
const boundary={...tip,boundary:true};r=selectMainPaths({graph:{...g,nodes:[a,b,c,d,boundary]}},500,334);assert.ok(r.nodes.some(n=>n.id==='tip'));
// Small parallel loop: keep the wider route, then merge the resulting degree-two chain.
const p=node('p',60,80),q=node('q',70,80),loop={nodes:[a,p,q,c],edges:[edge('ap',a,p),edge('pq',p,q,10),edge('noise',p,q,2,[[60,80],[60,86],[70,86],[70,80]]),edge('qc',q,c)]};
r=selectMainPaths({graph:loop},500,334);assert.equal(r.selection.removedCycles,1);assert.equal(r.edges.length,1);assert.deepEqual(r.edges[0].sourceEdgeIds,['ap','pq','qc']);assert.ok(!r.nodes.some(n=>n.id==='p'));
// Large actual ring is retained; empty and tiny-only inputs remain valid without invented lines.
const anchor=node('ring',40,40),ringPoints=[[40,40],[160,40],[160,160],[40,160],[40,40]];
r=selectMainPaths({graph:{nodes:[anchor],edges:[edge('ring-edge',anchor,anchor,8,ringPoints)]}},500,334);assert.equal(r.edges.length,1);assert.equal(r.edges[0].source,r.edges[0].target);
assert.equal(selectMainPaths({graph:{nodes:[],edges:[]}},500,334).edges.length,0);
const speckA=node('sa',300,300),speckB=node('sb',304,300);r=selectMainPaths({graph:{nodes:[...g.nodes,speckA,speckB],edges:[...g.edges,edge('speck',speckA,speckB,1)]}},500,334);assert.equal(graphStats(r).components,1);
// Retained paths use existing skeleton coordinates, endpoints match nodes, no new connections.
const byId=new Map(r.nodes.map(n=>[n.id,n]));for(const e of r.edges){assert.deepEqual(e.points[0],[byId.get(e.source).x,byId.get(e.source).y]);assert.deepEqual(e.points.at(-1),[byId.get(e.target).x,byId.get(e.target).y]);}
assert.deepEqual(selectMainPaths({graph:g},500,334),selectMainPaths({graph:g},500,334));
console.log('PASS: short spurs removed, real long branches and boundary exits kept, small loops choose wider route, big ring kept, degree-two chains merge, no added connections, full mode restores source.');
// A wide, short terminal chamber is a real destination even when shorter than the junction width.
const W=180,H=120,roomMask=new Uint8Array(W*H);
for(let y=0;y<H;y++)for(let x=0;x<W;x++)if((x>=10&&x<=170&&Math.abs(y-70)<=16)||Math.hypot(x-90,y-37)<=22||Math.hypot(x-90,y-70)<=16)roomMask[y*W+x]=1;
const ra=node('ra',15,70),rb=node('rb',90,70),rc=node('rc',165,70),rt=node('rt',90,37);
const rooms={mask:roomMask,graph:{nodes:[ra,rb,rc,rt],edges:[edge('ra-rb',ra,rb,32),edge('rb-rc',rb,rc,32),edge('room',rb,rt,36)]}};
r=selectMainPaths(rooms,W,H);assert.ok(r.nodes.some(n=>n.id==='rt'),'keep short route into a wide terminal chamber');
// Width-based pruning cannot erase a spatially large circuit after cavities have expanded.
const ringMask=new Uint8Array(200*200);for(let y=10;y<190;y++)for(let x=10;x<190;x++)if(x<55||x>145||y<55||y>145)ringMask[y*200+x]=1;
const ring=node('wide-ring',35,35),widePoints=[[35,35],[165,35],[165,165],[35,165],[35,35]];
r=selectMainPaths({mask:ringMask,graph:{nodes:[ring],edges:[edge('wide',ring,ring,100,widePoints)]}},200,200);assert.equal(r.edges.length,1,'preserve a real spatial circuit despite its large width');
console.log('PASS: broad chamber terminal and large cavity circuit survive pruning.');
