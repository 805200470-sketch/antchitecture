import assert from 'node:assert/strict';
import {analyzeMask,extractPaths,suggestROI,segment} from '../dist/engine.mjs';
const w=45,h=45,roi={x:0,y:0,width:w,height:h};
const make=()=>new Uint8Array(w*h);
const rect=(a,x,y,ww,hh)=>{for(let yy=y;yy<y+hh;yy++)for(let xx=x;xx<x+ww;xx++)a[yy*w+xx]=1;};
function verifyGraph(r){const nodes=new Map(r.graph.nodes.map(n=>[n.id,n]));for(const e of r.graph.edges){const a=nodes.get(e.source),b=nodes.get(e.target);assert.deepEqual(e.points[0],[a.x,a.y]);assert.deepEqual(e.points.at(-1),[b.x,b.y]);for(const p of e.points){assert.equal(r.mask[Math.floor(p[1])*w+Math.floor(p[0])],1);assert.ok(p.every(Number.isFinite));}assert.ok(e.lengthPx>0);}}
const t=make();rect(t,8,8,29,5);rect(t,20,8,5,29);let r=analyzeMask(t,w,h,roi);assert.equal(r.stats.components,1);assert.equal(r.stats.junctions,1);assert.equal(r.stats.endpoints,3);assert.equal(r.graph.edges.length,3);verifyGraph(r);
const ring=make();rect(ring,7,7,31,31);rect(ring,12,12,21,21);for(let y=12;y<33;y++)for(let x=12;x<33;x++)ring[y*w+x]=0;r=analyzeMask(ring,w,h,roi);assert.equal(r.stats.components,1);assert.equal(r.graph.edges.length,1);assert.equal(r.graph.edges[0].source,r.graph.edges[0].target);verifyGraph(r);
const border=make();rect(border,0,20,45,5);r=analyzeMask(border,w,h,roi);assert.equal(r.stats.components,1);assert.equal(r.graph.edges.length,1);assert.ok(r.graph.nodes.every(n=>n.boundary));verifyGraph(r);
const separate=make();rect(separate,4,4,4,25);rect(separate,30,4,4,25);r=analyzeMask(separate,w,h,roi);assert.equal(r.stats.components,2);assert.equal(r.graph.edges.length,2);verifyGraph(r);
const empty=analyzeMask(make(),w,h,roi);assert.equal(empty.stats.components,0);assert.equal(empty.graph.edges.length,0);
const rgba=new Uint8ClampedArray(w*h*4);for(let i=0;i<w*h;i++){const v=t[i]?20:180;rgba.set([v,v,v,255],i*4);}r=extractPaths({rgba,width:w,height:h,roi,options:{sensitivity:50,denoise:0,minArea:0}});assert.equal(r.stats.components,1);assert.equal(r.stats.junctions,1);assert.equal(r.stats.endpoints,3);verifyGraph(r);assert.deepEqual(r.mask,extractPaths({rgba,width:w,height:h,roi,options:{sensitivity:50,denoise:0,minArea:0}}).mask);
const cropped=analyzeMask(t,w,h,{x:0,y:0,width:18,height:45});for(let y=0;y<h;y++)for(let x=18;x<w;x++)assert.equal(cropped.mask[y*w+x],0);
const plain=new Uint8ClampedArray(w*h*4).fill(255);assert.equal(suggestROI(plain,w,h).found,false);assert.equal(segment(plain,w,h,roi,{denoise:0}).area,0);
console.log('PASS: T-junction, closed loop, border exits, disconnected paths, empty mask, deterministic extraction, ROI isolation, uniform image, graph coordinate continuity.');
