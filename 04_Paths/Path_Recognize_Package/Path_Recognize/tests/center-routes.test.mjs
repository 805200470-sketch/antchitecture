import assert from 'node:assert/strict';
import {centerRoutes} from '../dist/center-routes.mjs';
import {analyzeMask,suggestROI} from '../dist/engine.mjs';
import {segmentInside} from '../dist/path-smoothing.mjs';
const w=180,h=100,roi={x:0,y:0,width:w,height:h},mask=new Uint8Array(w*h);
for(let y=25;y<=75;y++)for(let x=10;x<=170;x++)mask[y*w+x]=1;
const points=[];for(let x=20;x<=160;x++)points.push([x+.5,50.5+Math.round(13*Math.sin(x*.18))]);
const nodes=[{id:'a',x:points[0][0],y:points[0][1],type:'endpoint',boundary:true,clusterPixels:[points[0]]},{id:'b',x:points.at(-1)[0],y:points.at(-1)[1],type:'endpoint',boundary:true,clusterPixels:[points.at(-1)]}];
const length=ps=>ps.slice(1).reduce((s,p,i)=>s+Math.hypot(p[0]-ps[i][0],p[1]-ps[i][1]),0);
const raw={mask,graph:{nodes,edges:[{id:'e',source:'a',target:'b',points,lengthPx:length(points),meanWidthPx:20}]}},before=JSON.stringify(raw.graph),beforeMask=mask.slice();
let r=centerRoutes(raw,w,h);assert.equal(r.edges.length,1);assert.ok(r.edges[0].lengthPx<length(points)*.72,'remove inherited winding detours');assert.deepEqual(r.edges[0].points[0],points[0]);assert.deepEqual(r.edges[0].points.at(-1),points.at(-1));
for(const e of r.edges)for(let i=1;i<e.points.length;i++)assert.ok(segmentInside(e.points[i-1],e.points[i],mask,w,h),'reroutes stay inside the original foreground');
assert.deepEqual(mask,beforeMask);assert.equal(JSON.stringify(raw.graph),before);
// A real soil wall forces a detour; it may not be filled to make a straight line.
const wall=mask.slice();for(let y=25;y<60;y++)for(let x=85;x<95;x++)wall[y*w+x]=0;
const original=analyzeMask(wall,w,h,roi);r=centerRoutes(original,w,h);for(const e of r.edges)for(const [x,y]of e.points)assert.ok(wall[Math.floor(y)*w+Math.floor(x)]);
// A manual full-width cut remains disconnected after rerouting.
for(let y=0;y<h;y++)for(let x=85;x<95;x++)wall[y*w+x]=0;
const cut=analyzeMask(wall,w,h,roi);r=centerRoutes(cut,w,h);assert.ok(r.edges.length>=2);for(const e of r.edges){const xs=e.points.map(p=>p[0]);assert.ok(Math.max(...xs)<85||Math.min(...xs)>=95);}
assert.deepEqual(centerRoutes(raw,w,h),centerRoutes(raw,w,h));
// Grayscale chamber rows must not be mistaken for a soil surface and crop away entrances.
const rgba=new Uint8ClampedArray(w*h*4);for(let y=0;y<h;y++)for(let x=0;x<w;x++){const j=(y*w+x)*4,v=y<25?245:100;rgba.set([v,v,v,255],j);}assert.deepEqual(suggestROI(rgba,w,h).roi,roi);
assert.deepEqual(centerRoutes({mask:new Uint8Array(w*h),graph:{nodes:[],edges:[]}},w,h).edges,[]);
console.log('PASS: texture detours rerouted through channel interiors, fixed boundary entrances, no mask expansion, soil walls and manual cuts respected, immutable original, deterministic, grayscale ROI preserves entrances.');
// A tiny texture hole at a branch attachment must not make a short in-room spur look like a new chamber.
const nw=300,nh=140,nroi={x:0,y:0,width:nw,height:nh},noisyMask=new Uint8Array(nw*nh);
for(let y=40;y<=100;y++)for(let x=20;x<=280;x++)noisyMask[y*nw+x]=1;
noisyMask[60*nw+148]=0;
const na={id:'na',x:40.5,y:60.5,type:'endpoint',boundary:true,clusterPixels:[[40.5,60.5]]},nb={id:'nb',x:150.5,y:60.5,type:'junction',boundary:false,clusterPixels:[[150.5,60.5]]},nc={id:'nc',x:260.5,y:60.5,type:'endpoint',boundary:true,clusterPixels:[[260.5,60.5]]},nt={id:'nt',x:150.5,y:83.5,type:'endpoint',boundary:false,clusterPixels:[[150.5,83.5]]};
const connect=(a,b)=>{const d=Math.max(Math.abs(b.x-a.x),Math.abs(b.y-a.y)),ps=Array.from({length:d+1},(_,i)=>[a.x+(b.x-a.x)*i/d,a.y+(b.y-a.y)*i/d]);return {id:a.id+b.id,source:a.id,target:b.id,points:ps,lengthPx:length(ps),meanWidthPx:20};};
const noisy={mask:noisyMask,graph:{nodes:[na,nb,nc,nt],edges:[connect(na,nb),connect(nb,nc),connect(nb,nt)]}};
const improved=centerRoutes(noisy,nw,nh);assert.equal(improved.edges.length,1,'prune the texture-rooted short in-room branch');assert.ok(improved.nodes.some(n=>n.id==='na')&&improved.nodes.some(n=>n.id==='nc'),'retain the main route and both entrances');
for(const e of improved.edges)for(let i=1;i<e.points.length;i++)assert.ok(segmentInside(e.points[i-1],e.points[i],noisyMask,nw,nh));
console.log('PASS: noisy narrow attachment no longer protects a false chamber spur; main entrances and foreground constraints survive.');
// A genuine long chamber destination must survive even though its width tapers at the closed end.
const rw=300,rh=140,room=new Uint8Array(rw*rh);
for(let y=0;y<rh;y++)for(let x=0;x<rw;x++)if(((x-180)/80)**2+((y-70)/35)**2<=1||(x<=180&&Math.abs(y-70)<=6)||(Math.abs(x-180)<=6&&y<=70))room[y*rw+x]=1;
const rn=(id,x,y,boundary=false)=>({id,x:x+.5,y:y+.5,type:'endpoint',boundary,clusterPixels:[[x+.5,y+.5]]});
const ra=rn('left',0,70,true),rb=rn('hub',160,70),rc=rn('entrance',180,0,true),rd=rn('chamber-tip',245,70);rb.type='junction';
const upPoints=[...Array.from({length:71},(_,i)=>[180.5,i+.5]),...Array.from({length:20},(_,i)=>[179.5-i,70.5])];
const chamberGraph={nodes:[ra,rb,rc,rd],edges:[connect(ra,rb),connect(rb,rd),{id:'entrance-hub',source:rc.id,target:rb.id,points:upPoints,lengthPx:length(upPoints),meanWidthPx:20}]};
const chamber=centerRoutes({mask:room,graph:chamberGraph},rw,rh);assert.ok(chamber.nodes.some(n=>n.id==='chamber-tip'),'preserve long closed chamber destination');assert.equal(chamber.edges.length,3);for(const e of chamber.edges)for(let i=1;i<e.points.length;i++)assert.ok(segmentInside(e.points[i-1],e.points[i],room,rw,rh));
console.log('PASS: a broad chamber retains its long terminal centerline and both entrances.');
