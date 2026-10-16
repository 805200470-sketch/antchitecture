import {distanceMap} from './engine.mjs';
import {selectMainPaths} from './main-paths.mjs';

const lengthOf=ps=>ps.slice(1).reduce((s,p,i)=>s+Math.hypot(p[0]-ps[i][0],p[1]-ps[i][1]),0);
const pixel=(p,w)=>Math.floor(p[1])*w+Math.floor(p[0]);

// Replace inherited texture detours with center-biased routes inside the existing mask.
// No dilation, gap filling or new foreground pixels are introduced here.
function route(points,mask,dist,w,h,scale) {
  const start=pixel(points[0],w),end=pixel(points.at(-1),w);
  if(start===end)return points;
  let peak=0,minX=w,minY=h,maxX=0,maxY=0;
  for(const p of points){peak=Math.max(peak,dist[pixel(p,w)]);minX=Math.min(minX,p[0]);maxX=Math.max(maxX,p[0]);minY=Math.min(minY,p[1]);maxY=Math.max(maxY,p[1]);}
  const pad=Math.ceil(Math.max(4*scale,peak)),x0=Math.max(0,Math.floor(minX)-pad),y0=Math.max(0,Math.floor(minY)-pad),x1=Math.min(w-1,Math.ceil(maxX)+pad),y1=Math.min(h-1,Math.ceil(maxY)+pad);
  const W=x1-x0+1,H=y1-y0+1,n=W*H,local=p=>(Math.floor(p/w)-y0)*W+p%w-x0;
  const costs=new Float64Array(n).fill(Infinity),parents=new Int32Array(n).fill(-1),heap=[];
  const push=item=>{let i=heap.length;heap.push(item);while(i){const p=(i-1)>>1;if(heap[p][0]<=item[0])break;heap[i]=heap[p];i=p;}heap[i]=item;};
  const pop=()=>{const first=heap[0],last=heap.pop();if(heap.length){let i=0;while(i*2+1<heap.length){let c=i*2+1;if(c+1<heap.length&&heap[c+1][0]<heap[c][0])c++;if(last[0]<=heap[c][0])break;heap[i]=heap[c];i=c;}heap[i]=last;}return first;};
  const tx=end%w,ty=Math.floor(end/w),heuristic=(x,y)=>Math.hypot(tx-x,ty-y);
  costs[local(start)]=0;push([heuristic(start%w,Math.floor(start/w)),0,start]);let found=false;
  while(heap.length){const [,cost,p]=pop(),lp=local(p);if(cost!==costs[lp])continue;if(p===end){found=true;break;}
    const x=p%w,y=Math.floor(p/w);
    for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
      if(!(dx||dy))continue;const xx=x+dx,yy=y+dy;if(xx<x0||xx>x1||yy<y0||yy>y1)continue;
      const q=yy*w+xx;if(!mask[q])continue;
      // A diagonal may follow existing corner-touching pixels, but cannot cut across a soil corner.
      if(dx&&dy&&(!mask[y*w+xx]||!mask[yy*w+x]))continue;
      const lq=local(q),step=dx&&dy?Math.SQRT2:1;
      const next=cost+step*(1+12*scale/(2*scale+(dist[p]+dist[q])/2));
      if(next>=costs[lq])continue;costs[lq]=next;parents[lq]=p;push([next+heuristic(xx,yy),next,q]);
    }
  }
  if(!found)return points;
  const chain=[];let p=end;
  while(p!==start){chain.push([p%w+.5,Math.floor(p/w)+.5]);p=parents[local(p)];if(p<0)return points;}
  chain.push(points[0]);chain.reverse();chain[chain.length-1]=points.at(-1);
  // Never replace a route with a much longer alternative around an unrelated obstacle.
  return lengthOf(chain)<=lengthOf(points)*1.15?chain:points;
}

function trimCurl(points,node,dist,w,scale) {
  if(node?.boundary||points.length<5)return 0;
  const total=lengthOf(points);let peak=0,walk=0;
  for(let i=0;i<points.length;i++){
    if(i)walk+=Math.hypot(points[i][0]-points[i-1][0],points[i][1]-points[i-1][1]);
    if(walk>total*.75)break;peak=Math.max(peak,dist[pixel(points[i],w)]);
  }
  if(peak<3*scale)return 0;
  const target=peak*.5;walk=0;
  for(let i=0;i<points.length;i++){
    if(i)walk+=Math.hypot(points[i][0]-points[i-1][0],points[i][1]-points[i-1][1]);
    if(dist[pixel(points[i],w)]>=target){
      const chord=Math.hypot(points[i][0]-points[0][0],points[i][1]-points[0][1]);
      // Keep narrow straight entrances; trim winding texture tails and very short tapered tips.
      return walk>chord*1.22||walk<peak*1.4?i:0;
    }
  }
  return 0;
}

export function centerRoutes(result,w,h,options={}) {
  const selected=selectMainPaths(result,w,h,'main');
  if(!result.mask||!selected.edges.length)return selected;
  const dist=distanceMap(result.mask,w,h),scale=Math.max(.5,Math.max(w,h)/500);
  const nodes=selected.nodes.map(n=>({...n,clusterPixels:n.clusterPixels?.map(p=>p.slice())??[]})),byId=new Map(nodes.map(n=>[n.id,n]));
  const degree=new Map(nodes.map(n=>[n.id,0]));for(const e of selected.edges){degree.set(e.source,degree.get(e.source)+1);degree.set(e.target,degree.get(e.target)+1);}
  let rerouted=0,trimmed=0;
  const edges=selected.edges.map(edge=>{
    let points=edge.points;const a=byId.get(edge.source),b=byId.get(edge.target);
    if(edge.source!==edge.target){
      const first=degree.get(edge.source)===1?trimCurl(points,a,dist,w,scale):0;
      const last=degree.get(edge.target)===1?trimCurl(points.slice().reverse(),b,dist,w,scale):0;
      if(first+last<points.length-2){
        points=points.slice(first,points.length-last);trimmed+=!!first+!!last;
        if(first){[a.x,a.y]=points[0];a.clusterPixels=[points[0]];}
        if(last){[b.x,b.y]=points.at(-1);b.clusterPixels=[points.at(-1)];}
      }
      const routed=route(points,result.mask,dist,w,h,scale);if(routed!==points)rerouted++;points=routed;
    }
    return {...edge,points,lengthPx:lengthOf(points),meanWidthPx:points.reduce((s,p)=>s+2*dist[pixel(p,w)],0)/points.length};
  });
  // Paths shortened by removing detours can now be recognized as small side spurs.
  const cleaned=selectMainPaths({...result,graph:{nodes,edges}},w,h,'main',{localCycleSpan:55*scale,preserveComponents:true,stableAttachments:options.stableAttachments!==false});
  const previous=new Map(edges.map(e=>[e.id,e]));
  for(const edge of cleaned.edges){
    edge.sourceEdgeIds=edge.sourceEdgeIds.flatMap(id=>previous.get(id)?.sourceEdgeIds??[id]);
    edge.centerlineMethod='mask-constrained-center-biased-geodesic';
    const inferred=edge.points.filter(p=>result.inferredMask?.[pixel(p,w)]).length;
    edge.inferredFraction=inferred/edge.points.length;edge.provenance=inferred?'contains-inference':'observed';
  }
  return {...cleaned,selection:{...selected.selection,method:'center-biased-geodesic-with-neighborhood-spur-pruning',reroutedEdges:rerouted,trimmedTextureTails:trimmed,
    postRouteRemovedEdges:cleaned.selection.removedEdges,postRouteRemovedSpurs:cleaned.selection.removedSpurs,postRouteRemovedCycles:cleaned.selection.removedCycles,sourceEdges:result.graph.edges.length,
    geometryRefined:true,maskExpanded:false,reviewRequired:true}};
}
