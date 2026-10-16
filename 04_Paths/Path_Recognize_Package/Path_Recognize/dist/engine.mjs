/** Deterministic, dependency-free image processing. All coordinates are processing-image pixels. */
import {reconstructGaps,annotateEvidence,terrainEvidence} from './reconstruction.mjs';
import {surfaceEvidence,growSurface,fuseSurfaceEvidence} from './surface-evidence.mjs';
import {centerRoutes} from './center-routes.mjs';
export const ENGINE_VERSION = '2.2.0';
const clamp = (v, lo=0, hi=255) => Math.max(lo,Math.min(hi,v));
const inside = (x,y,r) => x>=r.x && x<r.x+r.width && y>=r.y && y<r.y+r.height;
export function validateROI(roi,w,h) {
  if(!roi || ![roi.x,roi.y,roi.width,roi.height].every(Number.isFinite)) throw new Error('识别区域无效');
  const x=clamp(Math.round(roi.x),0,w-1), y=clamp(Math.round(roi.y),0,h-1);
  return {x,y,width:clamp(Math.round(roi.width),1,w-x),height:clamp(Math.round(roi.height),1,h-y)};
}
export function luminance(rgba) {
  const a=new Float32Array(rgba.length/4);
  for(let i=0,j=0;i<a.length;i++,j+=4) a[i]=.2126*rgba[j]+.7152*rgba[j+1]+.0722*rgba[j+2];
  return a;
}
export function suggestROI(rgba,w,h) {
  // Monochrome nest images have no reliable sky/soil color boundary; keep their entrances.
  let chromatic=0,samples=0;const stride=Math.max(1,Math.floor(w*h/12000));
  for(let i=0;i<w*h;i+=stride){const j=i*4;chromatic+=Math.max(rgba[j],rgba[j+1],rgba[j+2])-Math.min(rgba[j],rgba[j+1],rgba[j+2])>=12;samples++;}
  if(chromatic/Math.max(1,samples)<.03)return {roi:{x:0,y:0,width:w,height:h},found:false};
  const gray=luminance(rgba), row=new Float32Array(h), dif=new Float32Array(h);
  // A bounded downward luminance transition suggests the surface; user confirmation remains necessary.
  for(let y=0;y<h;y++){let sum=0;for(let x=0;x<w;x++)sum+=gray[y*w+x];row[y]=sum/w;}
  const span=Math.max(3,Math.round(h*.015));let best=0,cut=0;
  for(let y=Math.round(h*.08);y<Math.round(h*.48);y++){
    let a=0,b=0;for(let j=1;j<=span;j++){a+=row[Math.max(0,y-j)];b+=row[Math.min(h-1,y+j)];}
    dif[y]=(a-b)/span;if(dif[y]>best){best=dif[y];cut=y;}
  }
  if(best<12)return {roi:{x:0,y:0,width:w,height:h},found:false};
  const y=Math.max(0,cut-Math.round(h*.025));
  return {roi:{x:0,y,width:w,height:h-y},found:true};
}
function boxMean(a,w,h,r,roi){
  const stride=w+1, integral=new Float64Array(stride*(h+1)), result=new Float32Array(a.length);
  for(let y=0;y<h;y++){let sum=0;for(let x=0;x<w;x++){sum+=a[y*w+x];integral[(y+1)*stride+x+1]=integral[y*stride+x+1]+sum;}}
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){
    const x0=Math.max(roi.x,x-r),x1=Math.min(roi.x+roi.width,x+r+1),y0=Math.max(roi.y,y-r),y1=Math.min(roi.y+roi.height,y+r+1);
    result[y*w+x]=(integral[y1*stride+x1]-integral[y0*stride+x1]-integral[y1*stride+x0]+integral[y0*stride+x0])/((x1-x0)*(y1-y0));
  }return result;
}
function bilateral(a,w,h,level,roi){
  if(!level)return a;
  const r=level===1?1:2, sigma=12+level*7, weights=new Float32Array(256), out=a.slice();
  for(let d=0;d<256;d++)weights[d]=Math.exp(-d*d/(2*sigma*sigma));
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){
    const p=y*w+x,c=a[p];let sum=0,weight=0;
    for(let dy=-r;dy<=r;dy++)for(let dx=-r;dx<=r;dx++){
      const xx=clamp(x+dx,roi.x,roi.x+roi.width-1),yy=clamp(y+dy,roi.y,roi.y+roi.height-1),v=a[yy*w+xx];
      const k=weights[Math.round(Math.abs(v-c))]/(1+.35*(dx*dx+dy*dy));sum+=v*k;weight+=k;
    }out[p]=sum/weight;
  }return out;
}
function gaussian(a,w,h,level,roi){
  if(!level)return a;
  const sigma=level*.8,r=Math.ceil(sigma*2.5),k=[];let total=0;
  for(let j=-r;j<=r;j++){const v=Math.exp(-j*j/(2*sigma*sigma));k.push(v);total+=v;}for(let j=0;j<k.length;j++)k[j]/=total;
  const temp=a.slice(),out=a.slice();
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){let sum=0;for(let j=-r;j<=r;j++)sum+=a[y*w+clamp(x+j,roi.x,roi.x+roi.width-1)]*k[j+r];temp[y*w+x]=sum;}
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){let sum=0;for(let j=-r;j<=r;j++)sum+=temp[clamp(y+j,roi.y,roi.y+roi.height-1)*w+x]*k[j+r];out[y*w+x]=sum;}
  return out;
}
function thresholdOtsu(hist,total){
  let sum=0;for(let i=0;i<256;i++)sum+=i*hist[i];
  let sumB=0,wB=0,best=-1,threshold=0,last=0;
  for(let i=0;i<256;i++){wB+=hist[i];if(!wB)continue;const wF=total-wB;if(!wF)break;sumB+=i*hist[i];const d=sumB/wB-(sum-sumB)/wF,v=wB*wF*d*d;if(v>best+Math.max(1,best)*1e-10){best=v;threshold=last=i;}else if(Math.abs(v-best)<=Math.max(1,best)*1e-10)last=i;}
  return (threshold+last)/2;
}
function quantile(hist,n,q){let count=0;for(let i=0;i<256;i++){count+=hist[i];if(count>=n*q)return i;}return 255;}
function eachNeighbor(p,w,h,fn){const x=p%w,y=(p/w)|0;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++)if((dx||dy)&&x+dx>=0&&x+dx<w&&y+dy>=0&&y+dy<h)fn(p+dy*w+dx);}
function removeSmall(mask,w,h,minArea){
  const seen=new Uint8Array(mask.length), queue=new Int32Array(mask.length);let components=0,area=0;
  for(let i=0;i<mask.length;i++)if(mask[i]&&!seen[i]){
    let head=0,tail=1;queue[0]=i;seen[i]=1;
    while(head<tail){const p=queue[head++];eachNeighbor(p,w,h,n=>{if(mask[n]&&!seen[n]){seen[n]=1;queue[tail++]=n;}});}
    if(tail<minArea){for(let j=0;j<tail;j++)mask[queue[j]]=0;}else{components++;area+=tail;}
  }return {components,area};
}
function closeMask(mask,w,h,roi){
  const dil=new Uint8Array(mask.length),out=new Uint8Array(mask.length);
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){
    const p=y*w+x;let on=mask[p];eachNeighbor(p,w,h,n=>{on|=mask[n];});dil[p]=on;
  }
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){
    let on=1;for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){const xx=clamp(x+dx,roi.x,roi.x+roi.width-1),yy=clamp(y+dy,roi.y,roi.y+roi.height-1);on&=dil[yy*w+xx];}out[y*w+x]=on;
  }return out;
}
export function segment(rgba,w,h,roi,options={}){
  roi=validateROI(roi,w,h);
  const sensitivity=clamp(Number(options.sensitivity??50),0,100), contrast=clamp(Number(options.contrast??1.3),1,2), level=clamp(Number(options.denoise??2),0,3);
  const gray=bilateral(gaussian(luminance(rgba),w,h,level,roi),w,h,level,roi),mean=boxMean(gray,w,h,Math.max(10,Math.round(Math.min(roi.width,roi.height)*.09)),roi),enhanced=new Uint8ClampedArray(w*h),hist=new Uint32Array(256);
  for(let i=0;i<gray.length;i++)enhanced[i]=gray[i];
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){const p=y*w+x;enhanced[p]=clamp(gray[p]+(gray[p]-mean[p])*(contrast-1));hist[enhanced[p]]++;}
  const n=roi.width*roi.height, base=clamp(thresholdOtsu(hist,n),quantile(hist,n,.12),quantile(hist,n,.42)),shift=(sensitivity-50)*.7-25;
  let darkest=0,brightest=255;while(darkest<255&&!hist[darkest])darkest++;while(brightest>0&&!hist[brightest])brightest--;
  if(brightest-darkest<2)return {mask:new Uint8Array(w*h),enhanced,threshold:base,coverage:0,components:0,area:0};
  const strong=Math.max(darkest+.5,base-13+shift),weak=Math.max(strong+1,base+11+shift),candidate=new Uint8Array(w*h),queue=new Int32Array(w*h);let tail=0;
  let mask=new Uint8Array(w*h);
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){
    const p=y*w+x,v=enhanced[p],delta=mean[p]-gray[p];
    if(v<strong){mask[p]=1;candidate[p]=1;queue[tail++]=p;}
    else if((v<weak&&(delta>4||v<strong+8))||(delta>26&&v<weak+14))candidate[p]=1;
  }
  let head=0;while(head<tail){const p=queue[head++];eachNeighbor(p,w,h,n=>{if(candidate[n]&&!mask[n]){mask[n]=1;queue[tail++]=n;}});}
  if(options.closeGaps)mask=closeMask(mask,w,h,roi);
  const stats=removeSmall(mask,w,h,Math.max(0,Number(options.minArea??20)));
  return {mask,enhanced,threshold:base,coverage:stats.area/n,...stats};
}
/** Zhang–Suen thinning on a zero-padded image preserves paths touching the selected ROI. */
export function skeletonize(mask,w,h){
  const W=w+2,H=h+2,a=new Uint8Array(W*H),del=new Int32Array(w*h);
  for(let y=0;y<h;y++)a.set(mask.subarray(y*w,(y+1)*w),(y+1)*W+1);
  let changed=true,round=0;
  while(changed){changed=false;round++;if(round>Math.max(w,h)+1)throw new Error('骨架计算未收敛，请缩小识别区域');
    for(let pass=0;pass<2;pass++){
      let count=0;
      for(let y=1;y<H-1;y++)for(let x=1;x<W-1;x++){
        const i=y*W+x;if(!a[i])continue;
        const p2=a[i-W],p3=a[i-W+1],p4=a[i+1],p5=a[i+W+1],p6=a[i+W],p7=a[i+W-1],p8=a[i-1],p9=a[i-W-1];
        const B=p2+p3+p4+p5+p6+p7+p8+p9;if(B<2||B>6)continue;
        const A=(!p2&&p3)+(!p3&&p4)+(!p4&&p5)+(!p5&&p6)+(!p6&&p7)+(!p7&&p8)+(!p8&&p9)+(!p9&&p2);if(A!==1)continue;
        if(pass===0?(p2*p4*p6||p4*p6*p8):(p2*p4*p8||p2*p6*p8))continue;
        del[count++]=i;
      }
      if(count){changed=true;for(let i=0;i<count;i++)a[del[i]]=0;}
    }
  }
  const out=new Uint8Array(w*h);for(let y=0;y<h;y++)out.set(a.subarray((y+1)*W+1,(y+1)*W+1+w),y*w);return out;
}
// Two-pass chamfer distance: an explicit approximation, used only for width estimates.
export function distanceMap(mask,w,h){
  const d=new Float32Array(w*h),diag=Math.SQRT2;
  for(let i=0;i<d.length;i++)d[i]=mask[i]?1e6:0;
  for(let y=0;y<h;y++)for(let x=0;x<w;x++){const i=y*w+x;if(!mask[i])continue;let v=d[i];if(x===0||y===0||x===w-1||y===h-1)v=1;if(x)v=Math.min(v,d[i-1]+1);if(y)v=Math.min(v,d[i-w]+1);if(x&&y)v=Math.min(v,d[i-w-1]+diag);if(y&&x<w-1)v=Math.min(v,d[i-w+1]+diag);d[i]=v;}
  for(let y=h-1;y>=0;y--)for(let x=w-1;x>=0;x--){const i=y*w+x;if(!mask[i])continue;let v=d[i];if(x<w-1)v=Math.min(v,d[i+1]+1);if(y<h-1)v=Math.min(v,d[i+w]+1);if(x<w-1&&y<h-1)v=Math.min(v,d[i+w+1]+diag);if(x&&y<h-1)v=Math.min(v,d[i+w-1]+diag);d[i]=v;}return d;
}
function neighbors(s,p,w,h){
  const out=[],x=p%w,y=(p/w)|0;
  for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
    if(!(dx||dy)||x+dx<0||x+dx>=w||y+dy<0||y+dy>=h)continue;
    const n=p+dy*w+dx;if(!s[n])continue;
    // Avoid diagonal shortcuts where an orthogonal route already exists.
    if(dx&&dy&&(s[p+dx]||s[p+dy*w]))continue;out.push(n);
  }return out;
}
export function vectorize(s,w,h,roi,dist,mask=s){
  const ids=new Int32Array(s.length).fill(-1),degrees=new Uint8Array(s.length),nodes=[],edges=[];
  for(let p=0;p<s.length;p++)if(s[p])degrees[p]=neighbors(s,p,w,h).length;
  const touches=p=>{
    const x=p%w,y=(p/w)|0,radius=Math.ceil(dist?.[p]??1)+1;
    const rays=[[x-roi.x,-1],[roi.x+roi.width-1-x,1],[y-roi.y,-w],[roi.y+roi.height-1-y,w]];
    return rays.some(([length,step])=>{if(length>radius)return false;for(let i=0;i<=length;i++)if(!mask[p+i*step])return false;return true;});
  };
  function addNode(p,type,pixels=[p]){const id=nodes.length;for(const q of pixels)ids[q]=id;const rep=pixels[Math.floor(pixels.length/2)];nodes.push({id:'n'+id,x:rep%w+.5,y:Math.floor(rep/w)+.5,type,boundary:pixels.some(touches),pixels});return id;}
  for(let p=0;p<s.length;p++)if(s[p]&&degrees[p]>=3&&ids[p]<0){const group=[p];ids[p]=-2;for(let i=0;i<group.length;i++)for(const n of neighbors(s,group[i],w,h))if(degrees[n]>=3&&ids[n]===-1){ids[n]=-2;group.push(n);}addNode(p,'junction',group);}
  for(let p=0;p<s.length;p++)if(s[p]&&degrees[p]<=1)addNode(p,degrees[p]===0?'isolated':'endpoint');
  // Every cycle needs an anchor to be serializable as a self-loop.
  const seen=new Uint8Array(s.length);
  for(let p=0;p<s.length;p++)if(s[p]&&!seen[p]){const group=[p];seen[p]=1;let hasNode=false;for(let i=0;i<group.length;i++){const q=group[i];if(ids[q]>=0)hasNode=true;for(const n of neighbors(s,q,w,h))if(!seen[n]){seen[n]=1;group.push(n);}}if(!hasNode)addNode(p,'loop');}
  const visited=new Set(),key=(a,b)=>a<b?a*s.length+b:b*s.length+a;
  function withinNode(from,to,id){
    if(from===to)return [from];const queue=[from],parents=new Map([[from,-1]]);
    for(let i=0;i<queue.length;i++){const p=queue[i];for(const q of neighbors(s,p,w,h))if(ids[q]===id&&!parents.has(q)){parents.set(q,p);if(q===to){const out=[q];let n=p;while(n!==-1){out.push(n);n=parents.get(n);}return out.reverse();}queue.push(q);}}
    return [from];
  }
  for(let ni=0;ni<nodes.length;ni++){
    const node=nodes[ni];
    for(const start of node.pixels)for(const next of neighbors(s,start,w,h)){
      if(ids[next]===ni||visited.has(key(start,next)))continue;
      let prev=start,cur=next,chain=[start,next];visited.add(key(prev,cur));
      while(ids[cur]<0){const ns=neighbors(s,cur,w,h).filter(n=>n!==prev);if(!ns.length){addNode(cur,'endpoint');break;}const n=ns[0];if(visited.has(key(cur,n)))break;visited.add(key(cur,n));chain.push(n);prev=cur;cur=n;}
      const end=ids[cur];if(end<0)continue;
      // Extend traces through the junction's actual skeleton pixels so edge endpoints equal node coordinates.
      const representative=node.pixels[Math.floor(node.pixels.length/2)],endPixels=nodes[end].pixels,endRepresentative=endPixels[Math.floor(endPixels.length/2)];
      const lead=withinNode(representative,start,ni),tail=withinNode(cur,endRepresentative,end);
      chain=[...lead.slice(0,-1),...chain,...tail.slice(1)];
      const points=chain.map(p=>[p%w+.5,Math.floor(p/w)+.5]);let length=0,width=0;
      for(let i=0;i<points.length;i++){if(i)length+=Math.hypot(points[i][0]-points[i-1][0],points[i][1]-points[i-1][1]);width+=2*(dist?.[chain[i]]??0);}
      edges.push({id:'e'+edges.length,source:node.id,target:nodes[end].id,points,lengthPx:length,meanWidthPx:width/points.length,boundary:node.boundary||nodes[end].boundary});
    }
  }
  return {nodes:nodes.map(({pixels,...n})=>({...n,clusterPixels:pixels.map(p=>[p%w+.5,Math.floor(p/w)+.5])})),edges};
}
export function analyzeMask(input,w,h,roi){
  const mask=input.slice();roi=validateROI(roi,w,h);
  for(let y=0;y<h;y++)for(let x=0;x<w;x++)if(!inside(x,y,roi))mask[y*w+x]=0;
  const stats=removeSmall(mask,w,h,0),skeleton=skeletonize(mask,w,h),dist=distanceMap(mask,w,h),graph=vectorize(skeleton,w,h,roi,dist,mask);
  return {mask,skeleton,graph,stats:{...stats,segments:graph.edges.length,junctions:graph.nodes.filter(n=>n.type==='junction').length,endpoints:graph.nodes.filter(n=>n.type==='endpoint').length,boundaryNodes:graph.nodes.filter(n=>n.boundary).length,lengthPx:graph.edges.reduce((s,e)=>s+e.lengthPx,0),coverage:stats.area/(roi.width*roi.height)}};
}
export function extractPaths({rgba,width,height,roi,options={}}){
  if(rgba.length!==width*height*4)throw new Error('图像数据尺寸不匹配');
  roi=validateROI(roi,width,height);
  const segmented=segment(rgba,width,height,roi,options),gray=luminance(rgba);
  const surface=options.detectSurface===false?null:surfaceEvidence(gray,width,height,roi);
  const grown=surface?growSurface(segmented.mask,surface,width,height,roi,options):{mask:segmented.mask,added:0};
  const result=analyzeMask(grown.mask,width,height,roi),terrain=terrainEvidence(gray,width,height,roi);
  const field=surface?fuseSurfaceEvidence(terrain,surface):terrain;
  const reconstruction=reconstructGaps({gray,width,height,roi,result,field,options});
  let repaired=reconstruction.bridges.length?analyzeMask(reconstruction.mask,width,height,roi):result;
  let inferredMask=reconstruction.inferredMask,bridges=reconstruction.bridges;
  let refinementDiagnostics=[];
  if(options.reconstruct!==false){
    // Stable centerlines expose gaps hidden by texture endpoints. Keep prospective tips until after bridging.
    const candidates=centerRoutes(annotateEvidence(repaired,inferredMask,bridges,width),width,height,{stableAttachments:false});
    const refinement=reconstructGaps({gray,width,height,roi,result:{...repaired,graph:candidates},field,options:{...options,centerlinePass:true}});
    refinementDiagnostics=refinement.diagnostics;
    if(refinement.bridges.length){
      inferredMask=Uint8Array.from(inferredMask,(v,i)=>v||refinement.inferredMask[i]);
      bridges=[...bridges,...refinement.bridges].map((b,i)=>({...b,id:'bridge-'+i}));
      repaired=analyzeMask(refinement.mask,width,height,roi);
    }
  }
  return {...annotateEvidence(repaired,inferredMask,bridges,width),refinementDiagnostics,directMask:grown.mask,surfaceCue:surface?Float32Array.from(surface.paired,(v,i)=>v*Math.sqrt(surface.recess[i])):new Float32Array(width*height),surfaceDiagnostics:{enabled:!!surface,addedPixels:grown.added,initialComponents:segmented.components,recoveredComponents:result.stats.components,method:'multiscale-surface-closing-paired-shoulders-geodesic-growth',scoreIsProbability:false},depth:terrain.depth,relief:terrain.relief,ridge:terrain.ridge,enhanced:segmented.enhanced,threshold:segmented.threshold,reconstructionDiagnostics:reconstruction.diagnostics};
}
