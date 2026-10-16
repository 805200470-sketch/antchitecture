// Classical image cues. These are uncalibrated appearance scores, not physical depth.
const unit=v=>Math.max(0,Math.min(1,v));
const clamp=(v,a,b)=>Math.max(a,Math.min(b,v));

function box(a,w,h,r,roi){
  const W=w+1,integral=new Float64Array(W*(h+1)),out=a.slice();
  for(let y=0;y<h;y++){let row=0;for(let x=0;x<w;x++){row+=a[y*w+x];integral[(y+1)*W+x+1]=integral[y*W+x+1]+row;}}
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){
    const x0=Math.max(roi.x,x-r),x1=Math.min(roi.x+roi.width,x+r+1),y0=Math.max(roi.y,y-r),y1=Math.min(roi.y+roi.height,y+r+1);
    out[y*w+x]=(integral[y1*W+x1]-integral[y0*W+x1]-integral[y1*W+x0]+integral[y0*W+x0])/((x1-x0)*(y1-y0));
  }return out;
}
// Separable monotone-deque extrema, O(number of pixels) independent of radius.
function extrema(a,w,h,r,roi,isMax){
  const temp=a.slice(),out=a.slice(),deque=new Int32Array(Math.max(w,h));
  function line(src,dst,offset,stride,start,end){let head=0,tail=0,right=start;
    for(let x=start;x<end;x++){
      const wanted=Math.min(end-1,x+r);
      while(right<=wanted){const v=src[offset+right*stride];while(tail>head&&(isMax?src[offset+deque[tail-1]*stride]<=v:src[offset+deque[tail-1]*stride]>=v))tail--;deque[tail++]=right++;}
      while(tail>head&&deque[head]<x-r)head++;dst[offset+x*stride]=src[offset+deque[head]*stride];
    }
  }
  for(let y=roi.y;y<roi.y+roi.height;y++)line(a,temp,y*w,1,roi.x,roi.x+roi.width);
  for(let x=roi.x;x<roi.x+roi.width;x++)line(temp,out,x,w,roi.y,roi.y+roi.height);
  return out;
}
export function surfaceEvidence(gray,w,h,roi){
  const scale=Math.max(.65,Math.min(2.5,Math.max(w,h)/650)),radius=Math.max(1,Math.round(scale));
  const smooth=box(box(gray,w,h,radius,roi),w,h,radius,roi),fine=box(gray,w,h,1,roi),residual=new Float32Array(gray.length);
  for(let p=0;p<gray.length;p++)residual[p]=Math.abs(gray[p]-fine[p]);
  const noise=box(residual,w,h,Math.max(2,Math.round(4*scale)),roi);
  // Closing estimates the brighter surrounding surface across narrow and wide depressions.
  const narrow=extrema(extrema(smooth,w,h,Math.round(12*scale),roi,true),w,h,Math.round(12*scale),roi,false);
  const broad=extrema(extrema(smooth,w,h,Math.round(38*scale),roi,true),w,h,Math.round(38*scale),roi,false);
  const recess=new Float32Array(gray.length),paired=new Float32Array(gray.length),tx=new Float32Array(gray.length),ty=new Float32Array(gray.length),width=new Float32Array(gray.length);
  const at=(x,y)=>smooth[clamp(Math.round(y),roi.y,roi.y+roi.height-1)*w+clamp(Math.round(x),roi.x,roi.x+roi.width-1)];
  const directions=Array.from({length:12},(_,i)=>[Math.cos(i*Math.PI/12),Math.sin(i*Math.PI/12)]),radii=[2.5,4,6.5,10,16,25].map(r=>r*scale);
  for(let y=roi.y;y<roi.y+roi.height;y++)for(let x=roi.x;x<roi.x+roi.width;x++){
    const p=y*w+x,c=smooth[p],denom=22+noise[p]*.65;
    recess[p]=unit((Math.max(narrow[p]-c,(broad[p]-c)*.85)-3)/denom);
    if(recess[p]<.08)continue;
    let best=0,second=0,bestX=0,bestY=0,bestWidth=0;
    for(const r of radii)for(const [nx,ny]of directions){
      const dx=-ny,dy=nx,along=r*.7;
      const center=(c+at(x+dx*along,y+dy*along)+at(x-dx*along,y-dy*along))/3;
      const left=(at(x+nx*r,y+ny*r)+at(x+nx*r+dx*along,y+ny*r+dy*along)+at(x+nx*r-dx*along,y+ny*r-dy*along))/3;
      const right=(at(x-nx*r,y-ny*r)+at(x-nx*r+dx*along,y-ny*r+dy*along)+at(x-nx*r-dx*along,y-ny*r-dy*along))/3;
      const minimum=Math.min(left,right)-center;if(minimum<=1)continue;
      const outer=(left+right)/2-center,contrast=unit((.65*minimum+.35*outer-2)/(15+noise[p]*.5));
      const longitudinal=(Math.abs(at(x+dx*r,y+dy*r)-c)+Math.abs(at(x-dx*r,y-dy*r)-c))/2;
      const coherent=unit(outer/(outer+longitudinal*.6+1)),response=contrast*coherent;
      if(response>best){second=best;best=response;bestX=dx;bestY=dy;bestWidth=r*1.4;}else second=Math.max(second,response);
    }
    paired[p]=best*.75+second*.25;tx[p]=bestX;ty[p]=bestY;width[p]=bestWidth;
  }
  return {recess,paired,tx,ty,width,smooth,noise};
}

export function growSurface(seed,evidence,w,h,roi,options={}){
  const {recess,paired}=evidence,mask=seed.slice(),queue=new Int32Array(mask.length),distance=new Uint16Array(mask.length);let head=0,tail=0;
  for(let p=0;p<mask.length;p++)if(mask[p])queue[tail++]=p;
  const threshold=clamp(.34+(50-(options.sensitivity??50))*.003,.20,.49),maxGrowth=Math.ceil(Math.max(w,h)*.12);
  let added=0;
  while(head<tail){const p=queue[head++],x=p%w,y=Math.floor(p/w);if(distance[p]>=maxGrowth)continue;
    for(let dy=-1;dy<=1;dy++)for(let dx=-1;dx<=1;dx++){
      const xx=x+dx,yy=y+dy;if(!(dx||dy)||xx<roi.x||yy<roi.y||xx>=roi.x+roi.width||yy>=roi.y+roi.height)continue;
      const q=yy*w+xx;if(mask[q])continue;
      const supported=(recess[q]>=threshold&&paired[q]>=.46)||(paired[q]>=.64&&recess[q]>=.15);
      if(!supported)continue;mask[q]=1;distance[q]=distance[p]+1;queue[tail++]=q;added++;
    }
  }
  return {mask,added,threshold,maxGrowth};
}

// Keep the displayed relief unchanged. Additional profile evidence only informs gap routing.
export function fuseSurfaceEvidence(terrain,surface){
  const depth=terrain.depth.slice(),ridge=terrain.ridge.slice(),tx=terrain.tx.slice(),ty=terrain.ty.slice();
  for(let p=0;p<depth.length;p++){
    const support=surface.paired[p]*Math.sqrt(surface.recess[p]);
    depth[p]=Math.max(depth[p],support);
    if(support>ridge[p]){ridge[p]=support;tx[p]=surface.tx[p];ty[p]=surface.ty[p];}
  }
  return {...terrain,depth,ridge,tx,ty,surfaceRecess:surface.recess};
}
