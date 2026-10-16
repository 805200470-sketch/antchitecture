import {distanceMap} from './engine.mjs';

const lengthOf = points => points.slice(1).reduce((n,p,i)=>n+Math.hypot(p[0]-points[i][0],p[1]-points[i][1]),0);

function adjacency(graph, active) {
  const map = new Map(graph.nodes.map(n=>[n.id, []]));
  for (const i of active) { const e=graph.edges[i]; map.get(e.source)?.push(i); map.get(e.target)?.push(i); }
  return map;
}
function components(graph, active, adj) {
  const seen=new Set(), groups=[];
  for (const node of graph.nodes) {
    if (seen.has(node.id)||!adj.get(node.id)?.length) continue;
    const nodes=[node.id], edges=new Set();seen.add(node.id);
    for (let j=0;j<nodes.length;j++) for(const i of adj.get(nodes[j])) {
      edges.add(i);const e=graph.edges[i],other=e.source===nodes[j]?e.target:e.source;
      if(!seen.has(other)){seen.add(other);nodes.push(other);}
    }
    groups.push({nodes,edges});
  }
  return groups;
}
function shortest(graph, adj, start, excluded=-1, limit=Infinity) {
  const distances=new Map([[start,0]]), previous=new Map(), queue=[[0,start]];
  // Binary heap; bounds make local cycle queries independent of distant paths.
  const push=(item)=>{let i=queue.length;queue.push(item);while(i){const p=(i-1)>>1;if(queue[p][0]<=item[0])break;queue[i]=queue[p];i=p;}queue[i]=item;};
  const pop=()=>{const out=queue[0],last=queue.pop();if(queue.length){let i=0;while(i*2+1<queue.length){let c=i*2+1;if(c+1<queue.length&&queue[c+1][0]<queue[c][0])c++;if(last[0]<=queue[c][0])break;queue[i]=queue[c];i=c;}queue[i]=last;}return out;};
  while(queue.length){const [cost,id]=pop();if(cost!==distances.get(id))continue;
    for(const i of adj.get(id)??[]){if(i===excluded)continue;const e=graph.edges[i],other=e.source===id?e.target:e.source,next=cost+e.lengthPx;
      if(next>limit||next>=(distances.get(other)??Infinity))continue;
      distances.set(other,next);previous.set(other,{node:id,edge:i});push([next,other]);
    }
  }
  return {distances,previous};
}
function principal(graph, group, adj) {
  const ends=group.nodes.filter(id=>adj.get(id).length===1),candidates=ends.length?ends:group.nodes;
  // Repeated farthest-point sweeps find a stable long geodesic; not an exact longest simple path.
  let start=candidates[0],best={length:0,edges:[]};
  for(let pass=0;pass<4;pass++){
    const {distances,previous}=shortest(graph,adj,start);let end=start;
    for(const id of candidates)if((distances.get(id)??0)>(distances.get(end)??0))end=id;
    const length=distances.get(end)??0,edges=[];let at=end;
    while(at!==start&&previous.has(at)){const prev=previous.get(at);edges.push(prev.edge);at=prev.node;}
    if(length>best.length)best={length,edges};if(end===start)break;start=end;
  }
  // An unbranched closed channel has no distinct pair of graph nodes.
  if(!best.edges.length&&group.edges.size===1){const i=[...group.edges][0];best={length:graph.edges[i].lengthPx,edges:[i]};}
  return best;
}

function simplifyDegreeTwo(graph, active) {
  const adj=adjacency(graph,active),byId=new Map(graph.nodes.map(n=>[n.id,n]));
  const anchors=new Set(graph.nodes.filter(n=>adj.get(n.id).length!==2&&adj.get(n.id).length>0).map(n=>n.id));
  for(const group of components(graph,active,adj))if(!group.nodes.some(id=>anchors.has(id)))anchors.add(group.nodes[0]);
  const visited=new Set(),edges=[];
  for(const source of anchors)for(const first of adj.get(source)){
    if(visited.has(first))continue;let id=source,index=first,points=[],sourceEdgeIds=[],widthSum=0,lengthSum=0,inferredLength=0;
    while(!visited.has(index)){
      visited.add(index);const edge=graph.edges[index],forward=edge.source===id,part=forward?edge.points:edge.points.slice().reverse();
      for(let j=points.length?1:0;j<part.length;j++)points.push(part[j]);sourceEdgeIds.push(edge.id);
      widthSum+=(edge.meanWidthPx||1)*edge.lengthPx;lengthSum+=edge.lengthPx;inferredLength+=(edge.inferredFraction||0)*edge.lengthPx;
      id=forward?edge.target:edge.source;if(anchors.has(id))break;
      const next=adj.get(id).find(i=>!visited.has(i));if(next===undefined)break;index=next;
    }
    const fraction=lengthSum?inferredLength/lengthSum:0;
    edges.push({id:'main-'+edges.length,source,target:id,points,sourceEdgeIds,lengthPx:lengthOf(points),meanWidthPx:widthSum/(lengthSum||1),
      boundary:!!(byId.get(source)?.boundary||byId.get(id)?.boundary),inferredFraction:fraction,
      provenance:fraction?'contains-inferred':'direct-or-manually-edited',reviewRequired:true});
  }
  const ids=new Set(edges.flatMap(e=>[e.source,e.target]));
  const nodes=graph.nodes.filter(n=>ids.has(n.id)).map(n=>{
    const degree=edges.reduce((v,e)=>v+(e.source===n.id)+(e.target===n.id),0);
    return {...n,type:degree===1?'endpoint':degree>=3?'junction':'loop'};
  });
  return {nodes,edges};
}

// Estimate attachment width inside the same nearby foreground, avoiding a single noisy pixel.
function attachmentWidth(node,mask,dist,w,h,scale,span){
  const x=Math.floor(node.x),y=Math.floor(node.y),start=y*w+x,r=Math.ceil(Math.max(12*scale,Math.min(32*scale,span*.7))),seen=new Set([start]),queue=[start];let peak=dist[start];
  for(let i=0;i<queue.length;i++){const p=queue[i],xx=p%w,yy=Math.floor(p/w);peak=Math.max(peak,dist[p]);
    for(const [dx,dy]of [[1,0],[-1,0],[0,1],[0,-1]]){const nx=xx+dx,ny=yy+dy,q=ny*w+nx;
      if(nx<0||ny<0||nx>=w||ny>=h||(nx-x)**2+(ny-y)**2>r*r||seen.has(q)||!mask[q])continue;
      seen.add(q);queue.push(q);
    }
  }return 2*peak;
}

export function selectMainPaths(result,width,height,mode='main',options={}) {
  const graph=result.graph;
  if(mode==='full')return {...graph,selection:{mode,removedEdges:0,method:'unfiltered-skeleton'}};
  const scale=Math.max(.5,Math.max(width,height)/500),active=new Set(graph.edges.map((_,i)=>i));
  const dist=result.mask?distanceMap(result.mask,width,height):null;
  let adj=adjacency(graph,active);
  const groupMetrics=components(graph,active,adj).map(g=>{
    let mass=0,minX=Infinity,minY=Infinity,maxX=-Infinity,maxY=-Infinity;
    for(const i of g.edges){const e=graph.edges[i];mass+=e.lengthPx*Math.max(1,e.meanWidthPx||1);
      for(const [x,y]of e.points){minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);}}
    return {...g,mass,span:Math.hypot(maxX-minX,maxY-minY)};
  });
  const maxMass=Math.max(0,...groupMetrics.map(g=>g.mass));
  // Suppress tiny disconnected texture islands, but never join separate components.
  for(const g of groupMetrics)if(!options.preserveComponents&&(g.mass<Math.max(70*scale*scale,maxMass*.02)||g.span<16*scale))for(const i of g.edges)active.delete(i);
  adj=adjacency(graph,active);
  // Break only small local cycles, choosing the narrower, less supported side first.
  const candidates=[...active].sort((a,b)=>(graph.edges[a].meanWidthPx||1)-(graph.edges[b].meanWidthPx||1)||a-b);
  let removedCycles=0;
  for(const i of candidates){if(!active.has(i))continue;const e=graph.edges[i],limit=Math.max(40*scale,6*(e.meanWidthPx||1));
    const spanLimit=options.localCycleSpan??0,searchLimit=Math.max(limit,spanLimit*6);
    if(e.lengthPx>searchLimit)continue;
    const search=e.source===e.target?{distances:new Map([[e.target,0]]),previous:new Map()}:shortest(graph,adj,e.source,i,searchLimit-e.lengthPx);
    const alternate=search.distances.get(e.target);if(alternate===undefined)continue;
    let localCycle=false,largeSpatialCycle=false;
    if(spanLimit||dist){
      const cycle=[e];let at=e.target;
      while(at!==e.source&&search.previous.has(at)){const prev=search.previous.get(at);cycle.push(graph.edges[prev.edge]);at=prev.node;}
      let x0=Infinity,y0=Infinity,x1=-Infinity,y1=-Infinity;
      for(const edge of cycle)for(const [x,y]of edge.points){x0=Math.min(x0,x);x1=Math.max(x1,x);y0=Math.min(y0,y);y1=Math.max(y1,y);}
      const attached=cycle.some(edge=>adj.get(edge.source).length>2||adj.get(edge.target).length>2);
      const span=Math.hypot(x1-x0,y1-y0);
      localCycle=attached&&span<spanLimit;
      // Wide chambers must not turn a large, soil-enclosing circuit into a "small" loop.
      largeSpatialCycle=!!dist&&span>55*scale;
    }
    if(!largeSpatialCycle&&(alternate+e.lengthPx<=limit||localCycle)){active.delete(i);removedCycles++;adj=adjacency(graph,active);}
  }
  const protectedEdges=new Set();
  for(const g of components(graph,active,adj))for(const i of principal(graph,g,adj).edges)protectedEdges.add(i);
  const byId=new Map(graph.nodes.map(n=>[n.id,n]));
  let removedSpurs=0;
  for(let pass=0;pass<graph.edges.length;pass++){
    adj=adjacency(graph,active);const remove=new Set();
    for(const node of graph.nodes){if(adj.get(node.id).length!==1||node.boundary)continue;let id=node.id,prior=-1,total=0,chain=[];
      while(true){const i=adj.get(id).find(i=>i!==prior);if(i===undefined)break;const e=graph.edges[i];chain.push(i);total+=e.lengthPx;id=e.source===id?e.target:e.source;prior=i;if(adj.get(id).length!==2)break;}
      if(adj.get(id).length<3||(!options.stableAttachments&&chain.some(i=>protectedEdges.has(i))))continue;
      const end=byId.get(id),localWidth=dist?(options.stableAttachments?attachmentWidth(end,result.mask,dist,width,height,scale,total):2*dist[Math.floor(end.y)*width+Math.floor(end.x)]):Math.max(...adj.get(id).map(i=>graph.edges[i].meanWidthPx||1));
      const chord=Math.hypot(node.x-end.x,node.y-end.y);
      // A long, direct route well beyond the attachment is a destination, even in a wide chamber.
      if(total>=36*scale&&chord>=.9*localWidth&&total<=1.35*chord)continue;
      const terminalWidth=dist?2*dist[Math.floor(node.y)*width+Math.floor(node.x)]:0;
      if(terminalWidth>=Math.max(12*scale,localWidth*1.05))continue;
      if(total<Math.max(22*scale,1.65*localWidth))for(const i of chain)remove.add(i);
    }
    // If every arm is short inside one chamber, keep its principal through-route.
    if(options.stableAttachments)for(const group of components(graph,active,adj))if([...group.edges].every(i=>remove.has(i)))for(const i of group.edges)if(protectedEdges.has(i))remove.delete(i);
    if(!remove.size)break;for(const i of remove)active.delete(i);removedSpurs+=remove.size;
  }
  const selected=simplifyDegreeTwo(graph,active);
  return {...selected,selection:{mode,method:'width-aware-spur-and-local-cycle-pruning',sourceEdges:graph.edges.length,
    retainedSourceEdges:active.size,removedEdges:graph.edges.length-active.size,removedCycles,removedSpurs,
    topologySimplified:true,reviewRequired:true}};
}

export function graphStats(graph) {
  const active=new Set(graph.edges.map((_,i)=>i)),adj=adjacency(graph,active);
  return {components:components(graph,active,adj).length+graph.nodes.filter(n=>!adj.get(n.id).length).length,segments:graph.edges.length,
    junctions:graph.nodes.filter(n=>n.type==='junction').length,endpoints:graph.nodes.filter(n=>n.type==='endpoint').length,
    boundaryNodes:graph.nodes.filter(n=>n.boundary).length};
}
