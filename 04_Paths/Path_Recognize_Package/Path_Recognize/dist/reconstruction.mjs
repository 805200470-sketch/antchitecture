/** Image-supported 2.5D potential and conservative geodesic gap reconstruction.
 * The scalar field is a heuristic, never calibrated depth or a probability.
 */
const unit=v=>Math.max(0,Math.min(1,v));
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));
function blur(a,w,h,sigma,roi){
  const radius=Math.ceil(sigma*2.5),k=[];let total=0;
  for(let j=-radius;j<=radius;j++){const v=Math.exp(-j*j/(2*sigma*sigma));k.push(v);total+=v;}for(let j=0;j<k.length;j++)k[j]/=total;
  const temp=a.slice(),out=a.slice();
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){let v=0;for(let j=-radius;j<=radius;j++)v+=a[y*w+clamp(x+j,roi.x,roi.x+roi.width-1)]*k[j+radius];temp[y*w+x]=v;}
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){let v=0;for(let j=-radius;j<=radius;j++)v+=temp[clamp(y+j,roi.y,roi.y+roi.height-1)*w+x]*k[j+radius];out[y*w+x]=v;}return out;
}
function localMean(a,w,h,r,roi){
  const W=w+1,integral=new Float64Array(W*(h+1)),out=new Float32Array(a.length);
  for(let y=0;y<h;y++){let row=0;for(let x=0;x<w;x++){row+=a[y*w+x];integral[(y+1)*W+x+1]=integral[y*W+x+1]+row;}}
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){const x0=Math.max(roi.x,x-r),y0=Math.max(roi.y,y-r),x1=Math.min(roi.x+roi.width,x+r+1),y1=Math.min(roi.y+roi.height,y+r+1);out[y*w+x]=(integral[y1*W+x1]-integral[y0*W+x1]-integral[y1*W+x0]+integral[y0*W+x0])/((x1-x0)*(y1-y0));}return out;
}
export function terrainEvidence(gray,w,h,roi){
  const n=w*h,ridge=new Float32Array(n),tx=new Float32Array(n),ty=new Float32Array(n),scale=new Float32Array(n),smooth=blur(gray,w,h,1.1,roi),background=localMean(smooth,w,h,Math.max(12,Math.round(Math.min(roi.width,roi.height)*.065)),roi),depth=new Float32Array(n);
  const scaleFactor=Math.max(.75,Math.min(2,Math.max(w,h)/650));
  for(const rawSigma of [1,2.2,4.4]){
    const sigma=rawSigma*scaleFactor,a=blur(gray,w,h,sigma,roi),d=Math.max(1,Math.round(sigma)),factor=sigma*sigma/(d*d);
    const at=(x,y)=>a[clamp(Math.round(y),roi.y,roi.y+roi.height-1)*w+clamp(Math.round(x),roi.x,roi.x+roi.width-1)];
    for(let y=roi.y+d;y<roi.y+roi.height-d;y++)for(let x=roi.x+d;x<roi.x+roi.width-d;x++){
      const p=y*w+x,c=a[p],xx=(at(x+d,y)-2*c+at(x-d,y))*factor,yy=(at(x,y+d)-2*c+at(x,y-d))*factor,xy=(at(x+d,y+d)-at(x+d,y-d)-at(x-d,y+d)+at(x-d,y-d))*.25*factor;
      const delta=Math.hypot(xx-yy,2*xy),normal=(xx+yy+delta)/2,tangent=(xx+yy-delta)/2;if(normal<=0||Math.abs(tangent)>normal)continue;
      const angle=.5*Math.atan2(2*xy,xx-yy),nx=Math.cos(angle),ny=Math.sin(angle),side=2.2*sigma;
      const left=at(x+nx*side,y+ny*side)-c,right=at(x-nx*side,y-ny*side)-c;
      const paired=unit(Math.min(left,right)/14),coherence=Math.exp(-Math.pow(tangent/normal,2)/.32),response=(1-Math.exp(-normal*normal/60))*coherence*(.25+.75*paired);
      if(response>ridge[p]){ridge[p]=response;tx[p]=-ny;ty[p]=nx;scale[p]=sigma;}
    }
  }
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){const p=y*w+x,contrast=unit((background[p]-smooth[p])/35);depth[p]=unit(.55*contrast+.45*ridge[p]);}
  return {depth,relief:blur(depth,w,h,2.2*scaleFactor,roi),ridge,tx,ty,scale,smooth,background};
}
function labelRegions(mask,w,h){const labels=new Int32Array(mask.length),queue=new Int32Array(mask.length);let id=0;for(let p=0;p<mask.length;p++)if(mask[p]&&!labels[p]){id++;let head=0,tail=1;queue[0]=p;labels[p]=id;while(head<tail){const q=queue[head++],x=q%w,y=(q/w)|0;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){if((!dx&&!dy)||x+dx<0||x+dx>=w||y+dy<0||y+dy>=h)continue;const next=q+dy*w+dx;if(mask[next]&&!labels[next]){labels[next]=id;queue[tail++]=next;}}}}return {labels,count:id};}
class MinHeap{constructor(){this.a=[];}push(item){let i=this.a.length;this.a.push(item);while(i){const parent=(i-1)>>1;if(this.a[parent][0]<=item[0])break;this.a[i]=this.a[parent];i=parent;}this.a[i]=item;}pop(){const first=this.a[0],last=this.a.pop();if(this.a.length){let i=0;while(i*2+1<this.a.length){let child=i*2+1;if(child+1<this.a.length&&this.a[child+1][0]<this.a[child][0])child++;if(this.a[child][0]>=last[0])break;this.a[i]=this.a[child];i=child;}this.a[i]=last;}return first;}get length(){return this.a.length;}}
function geodesic(a,b,field,w,h,roi,labels,maxGap,weight=1){
  const pad=Math.ceil(maxGap*.28),x0=Math.max(roi.x,Math.min(a.x,b.x)-pad),y0=Math.max(roi.y,Math.min(a.y,b.y)-pad),x1=Math.min(roi.x+roi.width-1,Math.max(a.x,b.x)+pad),y1=Math.min(roi.y+roi.height-1,Math.max(a.y,b.y)+pad),W=x1-x0+1,H=y1-y0+1,n=W*H;
  const from=(a.y-y0)*W+a.x-x0,to=(b.y-y0)*W+b.x-x0,cost=new Float64Array(n).fill(Infinity),parent=new Int32Array(n).fill(-1),closed=new Uint8Array(n),heap=new MinHeap();cost[from]=0;heap.push([0,from]);let visited=0;
  while(heap.length){const [,p]=heap.pop();if(closed[p])continue;closed[p]=1;if(p===to)break;if(++visited>n)break;const x=p%W+x0,y=((p/W)|0)+y0;
    for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
      if(!(dx||dy)||x+dx<x0||x+dx>x1||y+dy<y0||y+dy>y1)continue;
      const xx=x+dx,yy=y+dy,q=(yy-y0)*W+xx-x0,g=yy*w+xx;if(closed[q])continue;
      const region=labels[g];if(region&&region!==a.component&&region!==b.component)continue;
      const distance=Math.hypot(dx,dy),support=field.depth[g],ridge=field.ridge[g],align=Math.abs(dx/distance*field.tx[g]+dy/distance*field.ty[g]);
      const bright=unit((field.smooth[g]-field.background[g]+3)/22);
      const step=distance*(1+7*weight*(1-support)**2+4*bright+2/weight*ridge*(1-align));
      const v=cost[p]+step;if(v<cost[q]){cost[q]=v;parent[q]=p;heap.push([v+Math.hypot(xx-b.x,yy-b.y),q]);}
    }
  }
  if(!Number.isFinite(cost[to]))return null;const path=[];let p=to;while(p>=0){path.push(((p/W)|0)+y0);path.push(p%W+x0);if(p===from)break;p=parent[p];}const points=[];for(let i=path.length-2;i>=0;i-=2)points.push([path[i+1],path[i]]);return points;
}
function endpointGeometry(node,graph,w){
  const edge=graph.edges.find(e=>e.source===node.id||e.target===node.id);if(!edge||edge.lengthPx<5)return null;
  const points=edge.source===node.id?edge.points:[...edge.points].reverse();let length=0,index=0;while(index<points.length-1&&length<9){length+=Math.hypot(points[index+1][0]-points[index][0],points[index+1][1]-points[index][1]);index++;}
  const dx=points[0][0]-points[index][0],dy=points[0][1]-points[index][1],mag=Math.hypot(dx,dy);if(!mag)return null;
  return {nodeId:node.id,x:Math.floor(node.x),y:Math.floor(node.y),dx:dx/mag,dy:dy/mag,width:edge.meanWidthPx,p:Math.floor(node.y)*w+Math.floor(node.x)};
}
function scoreRoute(points,a,b,field,mask,w){
  const gap=points.filter(([x,y])=>!mask[y*w+x]);if(!gap.length)return null;
  let sum=0,weak=0,longest=0,run=0,ridge=0,length=0,bright=0,flank=0,flankCount=0,recess=0;
  for(let i=0;i<points.length;i++){const [x,y]=points[i],p=y*w+x;if(i)length+=Math.hypot(x-points[i-1][0],y-points[i-1][1]);if(mask[p]){if(points.slice(Math.max(0,i-5),i+6).some(([xx,yy])=>!mask[yy*w+xx])){flank+=field.depth[p];flankCount++;}continue;}recess+=field.surfaceRecess?.[p]??0;if((!field.surfaceRecess||field.surfaceRecess[p]<.12)&&field.smooth[p]>field.background[p]+8)bright++;const evidence=field.depth[p];sum+=evidence;ridge+=field.ridge[p];if(evidence<.24){weak++;run++;longest=Math.max(run,longest);}else run=0;}
  let gapLengthPx=0;
  for(let i=1;i<points.length;i++){const [x,y]=points[i],[px,py]=points[i-1];if(!mask[y*w+x]||!mask[py*w+px])gapLengthPx+=Math.hypot(x-px,y-py);}
  const mean=sum/gap.length,weakFraction=weak/gap.length,detour=length/Math.max(1,Math.hypot(b.x-a.x,b.y-a.y));
  const near=points[Math.min(points.length-1,5)],far=points[Math.max(0,points.length-6)],out1=[near[0]-a.x,near[1]-a.y],out2=[far[0]-b.x,far[1]-b.y];
  const directionA=(out1[0]*a.dx+out1[1]*a.dy)/(Math.hypot(...out1)||1),directionB=b.dx==null?1:(out2[0]*b.dx+out2[1]*b.dy)/(Math.hypot(...out2)||1);
  const supportScore=unit(.55*mean+.2*(1-weakFraction)+.15*unit((directionA+directionB)/2)+.1*unit(2-detour));
  return {supportScore,meanEvidence:mean,meanRidge:ridge/gap.length,flankEvidence:flank/(flankCount||1),meanRecess:recess/gap.length,brightFraction:bright/gap.length,weakFraction,longestWeakRun:longest,detour,directionA,directionB,gapPixels:gap.length,gapLengthPx,lengthPx:length};
}
function profileRoute(points,a,b,gray,mask,w,roi){
  // Recheck the original image: blur alone can turn a narrow bright wall into a false valley.
  const patch=(x,y)=>{let sum=0;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)sum+=gray[clamp(Math.round(y)+dy,roi.y,roi.y+roi.height-1)*w+clamp(Math.round(x)+dx,roi.x,roi.x+roi.width-1)];return sum/9;};
  let flat=0,flatRun=0,longestFlatRun=0;
  for(let i=0;i<points.length;i++){
    const [x,y]=points[i];if(mask[y*w+x]){flatRun=0;continue;}
    const p=points[Math.max(0,i-3)],q=points[Math.min(points.length-1,i+3)],d=Math.hypot(q[0]-p[0],q[1]-p[1])||1,nx=-(q[1]-p[1])/d,ny=(q[0]-p[0])/d,c=patch(x,y);
    const radius=Math.max(4,Math.min(30,(a.width+b.width)/4));let contrast=-Infinity;
    for(const factor of [1,1.5,2])contrast=Math.max(contrast,Math.min(patch(x+nx*radius*factor,y+ny*radius*factor),patch(x-nx*radius*factor,y-ny*radius*factor))-c);
    if(contrast<3){flat++;flatRun++;longestFlatRun=Math.max(longestFlatRun,flatRun);}else flatRun=0;
  }
  const gaps=points.reduce((n,[x,y])=>n+!mask[y*w+x],0);
  return {profileFlatFraction:flat/Math.max(1,gaps),profileLongestFlatRun:longestFlatRun};
}
export function reconstructGaps({gray,width:w,height:h,roi,result,field:provided,options={}}){
  const field=provided??terrainEvidence(gray,w,h,roi),mask=result.mask.slice(),inferredMask=new Uint8Array(w*h),{labels,count}=labelRegions(mask,w,h),maxGap=clamp(Math.round(options.maxGap??42),4,100),candidates=[],accepted=[],diagnostics=[];
  if(options.reconstruct===false)return {mask,inferredMask,bridges:accepted,field,diagnostics};
  const reach=(a,b)=>maxGap+(options.centerlinePass?Math.min(120,a.width+b.width):0);
  const endpoints=result.graph.nodes.filter(n=>n.type==='endpoint'&&!n.boundary).map(n=>endpointGeometry(n,result.graph,w)).filter(Boolean).sort((a,b)=>b.width-a.width).slice(0,256).map(e=>({...e,component:labels[e.p]}));
  for(let i=0;i<endpoints.length;i++){
    const a=endpoints[i],nearest=[];
    for(let j=i+1;j<endpoints.length;j++){const b=endpoints[j],dx=b.x-a.x,dy=b.y-a.y,d=Math.hypot(dx,dy);if(d>reach(a,b)||d<2)continue;const alignmentA=(dx*a.dx+dy*a.dy)/d,alignmentB=(-dx*b.dx-dy*b.dy)/d;if(alignmentA<.12||alignmentB<.12)continue;if(Math.max(a.width,b.width)/Math.max(1,Math.min(a.width,b.width))>4)continue;nearest.push({a,b,d,kind:'endpoint'});}
    // Endpoint-to-trunk candidates allow supported T junctions without forcing every component to join.
    const trunks=new Map();
    for(const edge of result.graph.edges){if(edge.lengthPx<12)continue;for(let j=3;j<edge.points.length-3;j+=3){const [fx,fy]=edge.points[j],x=Math.floor(fx),y=Math.floor(fy),component=labels[y*w+x];if(component===a.component)continue;const dx=x-a.x,dy=y-a.y,d=Math.hypot(dx,dy);if(d<3||d>reach(a,{width:edge.meanWidthPx})||(dx*a.dx+dy*a.dy)/d<.68)continue;const old=trunks.get(component);if(!old||d<old.d)trunks.set(component,{a,b:{x,y,component,width:edge.meanWidthPx},d,kind:'trunk'});}}
    nearest.push(...trunks.values());nearest.sort((a,b)=>a.d-b.d);candidates.push(...nearest.slice(0,5));
  }
  for(const candidate of candidates){const {a,b}=candidate,points=geodesic(a,b,field,w,h,roi,labels,maxGap);if(!points)continue;const evidence=scoreRoute(points,a,b,field,mask,w);if(!evidence)continue;
    const shortSupported=evidence.gapPixels<=6&&evidence.meanEvidence>=.27&&evidence.meanRidge>=.50;
    const flankSupported=!!options.centerlinePass&&evidence.gapPixels<=6&&evidence.meanEvidence>=.24&&evidence.meanRidge>=.24&&evidence.flankEvidence>=.5&&evidence.meanRecess>=.2&&evidence.brightFraction===0&&evidence.longestWeakRun<=2&&evidence.detour<=1.15&&evidence.directionA>=.65&&evidence.directionB>=.65;
    evidence.rule=flankSupported?'short-gap-with-stable-flanks':shortSupported?'short-ridge-gap':'continuous-ridge';
    let valid=(evidence.gapLengthPx<=maxGap)&&(flankSupported||shortSupported||(evidence.meanEvidence>=.34&&evidence.meanRidge>=.5))&&evidence.weakFraction<=.30&&evidence.longestWeakRun<=Math.max(3,Math.min(5,evidence.gapPixels*.25))&&evidence.detour<=1.5&&evidence.directionA>=.45&&evidence.directionB>=.4&&evidence.supportScore>=(flankSupported?.50:shortSupported?.53:.56);
    if(valid){Object.assign(evidence,profileRoute(points,a,b,gray,mask,w,roi));valid=!(evidence.profileLongestFlatRun>=3&&evidence.profileFlatFraction>.35);}
    if(valid){const alternate=geodesic(a,b,field,w,h,roi,labels,maxGap,1.18);let deviation=0;for(const p of points)deviation=Math.max(deviation,Math.min(...alternate.map(q=>Math.hypot(p[0]-q[0],p[1]-q[1]))));evidence.stabilityDeviationPx=deviation;valid=deviation<=(flankSupported?1:3);}
    diagnostics.push({from:[a.x,a.y],to:[b.x,b.y],valid,...evidence});if(valid)candidate.points=points,candidate.evidence=evidence;
  }
  const parent=Array.from({length:count+1},(_,i)=>i),find=a=>{while(parent[a]!==a){parent[a]=parent[parent[a]];a=parent[a];}return a;},used=new Set();
  candidates.filter(c=>c.points).sort((a,b)=>b.evidence.supportScore-a.evidence.supportScore||a.d-b.d).forEach(c=>{
    if(used.has(c.a.nodeId)||(c.b.nodeId&&used.has(c.b.nodeId))||(c.kind==='trunk'&&find(c.a.component)===find(c.b.component)))return;
    const radius=Math.max(1,Math.min(3,Math.min(c.a.width,c.b.width)*.16));
    const proposed=new Set();
    for(const [cx,cy]of c.points)for(let y=Math.max(roi.y,Math.floor(cy-radius));y<=Math.min(roi.y+roi.height-1,Math.ceil(cy+radius));y++)for(let x=Math.max(roi.x,Math.floor(cx-radius));x<=Math.min(roi.x+roi.width-1,Math.ceil(cx+radius));x++)if((x-cx)**2+(y-cy)**2<=radius*radius&&(x===cx&&y===cy||field.depth[y*w+x]>.32)){const p=y*w+x;if(!mask[p])proposed.add(p);}
    const roots=[find(c.a.component),find(c.b.component)];let conflict=false;
    for(const p of proposed){const x=p%w,y=(p/w)|0;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){if(x+dx<0||x+dx>=w||y+dy<0||y+dy>=h)continue;const label=labels[p+dy*w+dx];if(label&&!roots.includes(find(label)))conflict=true;}}
    if(conflict)return;
    for(const p of proposed){mask[p]=1;inferredMask[p]=1;}
    parent[find(c.a.component)]=find(c.b.component);used.add(c.a.nodeId);if(c.b.nodeId)used.add(c.b.nodeId);
    accepted.push({id:'bridge-'+accepted.length,kind:c.kind,points:c.points.map(([x,y])=>[x+.5,y+.5]),evidence:c.evidence,provenance:'inferred',stage:options.centerlinePass?'centerline-refinement':'raw-skeleton',reviewRequired:true});
  });
  return {mask,inferredMask,bridges:accepted,field,diagnostics};
}
export function annotateEvidence(result,inferredMask,bridges,w){
  const provenance=inferredMask?.slice()??new Uint8Array(result.mask.length);for(let i=0;i<provenance.length;i++)if(!result.mask[i])provenance[i]=0;
  result.graph.edges=result.graph.edges.map(edge=>{let count=0;for(const [x,y]of edge.points)if(provenance[Math.floor(y)*w+Math.floor(x)])count++;return {...edge,inferredFraction:count/edge.points.length,provenance:count?'contains-inference':'observed',reviewRequired:count>0};});
  return {...result,inferredMask:provenance,bridges:bridges??[]};
}
