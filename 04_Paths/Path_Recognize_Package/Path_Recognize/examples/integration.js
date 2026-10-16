import {t,initI18n} from '../dist/i18n.mjs';
import {acceptNetwork} from './downstream.mjs';
const $=id=>document.getElementById(id),canvas=$('preview'),ctx=canvas.getContext('2d');
let worker=null,jobId=0,packet=null,originalPixels=null;
const demo=window.integrationDemo={packet:null,received:null,busy:false,error:null};
let lastStatus={key:'选择图片开始。此示例使用全图识别。',params:{}};
function status(key,params={}){lastStatus={key,params};$('status').textContent=t(key,params);}
function busy(value){demo.busy=value;$('cancel').disabled=!value;$('inference').disabled=value||!packet;$('download').disabled=value||!packet;}
function stop(){jobId++;worker?.terminate();worker=null;busy(false);}
function onNetworkReady(network){
  // Replace or extend this callback to connect the actual next workflow.
  demo.received=acceptNetwork(network);
  const degrees=[...demo.received.adjacency.values()].map(edges=>edges.length),edge=network.edges[0];
  const exampleEdge=edge?{id:edge.id,source:edge.source,target:edge.target,pointCount:edge.points.length,firstPoint:edge.points[0],lastPoint:edge.points.at(-1),lengthPx:edge.lengthPx}:null;
  $('summary').textContent=JSON.stringify({schemaVersion:network.schemaVersion,image:network.image,nodes:network.nodes.length,edges:network.edges.length,connectedComponents:network.stats.components,adjacencyEntries:degrees.length,inferred:network.quality.includesInferredConnections,exampleEdge},null,2);
  window.dispatchEvent(new CustomEvent('path-recognize:ready',{detail:{network}}));
}
function draw(network){
  ctx.putImageData(originalPixels,0,0);
  const sx=canvas.width/network.image.width,sy=canvas.height/network.image.height;
  ctx.lineWidth=Math.max(1.5,canvas.width/600);ctx.lineCap='round';ctx.lineJoin='round';
  for(const edge of network.edges)for(let i=1;i<edge.points.length;i++){
    const inferred=edge.inferredSegments[i-1];ctx.strokeStyle=inferred?'#ffc16c':'#9ff4cf';ctx.setLineDash(inferred?[5,4]:[]);
    ctx.beginPath();ctx.moveTo(edge.points[i-1][0]*sx,edge.points[i-1][1]*sy);ctx.lineTo(edge.points[i][0]*sx,edge.points[i][1]*sy);ctx.stroke();
  }
  ctx.setLineDash([]);
}
function run(kind,data){
  worker?.terminate();const id=++jobId;demo.error=null;busy(true);status('正在识别并校验网络接口…');
  worker=new Worker(new URL('../dist/integration-worker.mjs',import.meta.url),{type:'module'});
  const fail=message=>{if(id!==jobId)return;worker?.terminate();worker=null;demo.error=message;busy(false);status('处理失败：{message}',{message:t(message)});};
  worker.onerror=e=>fail(e.message||'后台处理失败');
  worker.onmessage=({data:reply})=>{
    if(id!==jobId||reply.id!==id)return;
    if(!reply.ok){fail(reply.error.message);return;}
    worker.terminate();worker=null;
    try{
      if(kind==='recognize')packet=reply.result;
      else packet={...packet,network:reply.result,context:{...packet.context,includeInference:$('inference').checked}};
      onNetworkReady(packet.network);demo.packet=packet;draw(packet.network);busy(false);
      status('已接收 {nodes} 个节点、{edges} 条中心线；连接引用与坐标校验通过。',{nodes:packet.network.nodes.length,edges:packet.network.edges.length});
    }catch(error){fail(error.message);}
  };
  // Structured clone keeps caller-owned input/packet buffers usable for later editing.
  worker.postMessage({id,kind,...data});
}
async function loadImage(file){
  stop();const id=jobId;packet=null;demo.packet=null;demo.received=null;demo.error=null;busy(true);$('summary').textContent=t('等待 network…');status('正在读取图片…');
  try{
    if(file.size>25*1024*1024)throw new Error('图片需小于 25 MB');
    const bitmap=await createImageBitmap(file);
    if(id!==jobId){bitmap.close();return;}
    const originalWidth=bitmap.width,originalHeight=bitmap.height;
    if(originalWidth*originalHeight>60000000){bitmap.close();throw new Error('图片需小于 6000 万像素');}
    const scale=Math.min(1,1400/Math.max(originalWidth,originalHeight));
    canvas.width=Math.max(1,Math.round(originalWidth*scale));canvas.height=Math.max(1,Math.round(originalHeight*scale));
    ctx.fillStyle='#fff';ctx.fillRect(0,0,canvas.width,canvas.height);ctx.drawImage(bitmap,0,0,canvas.width,canvas.height);bitmap.close();
    originalPixels=ctx.getImageData(0,0,canvas.width,canvas.height);
    run('recognize',{input:{rgba:originalPixels.data,width:canvas.width,height:canvas.height,image:{name:file.name,width:originalWidth,height:originalHeight},includeInference:$('inference').checked}});
  }catch(error){if(id===jobId){demo.error=error.message;busy(false);status('无法读取：{message}',{message:t(error.message)});}}
}
async function sample(url,name){
  stop();const id=jobId;busy(true);status('正在读取示例…');
  try{const response=await fetch(url);if(!response.ok)throw new Error('示例读取失败');const blob=await response.blob();if(id===jobId)await loadImage(new File([blob],name,{type:blob.type}));}
  catch(error){if(id===jobId){demo.error=error.message;busy(false);status(error.message);}}
}
$('choose-image').onclick=()=>$('file').click();
$('file').onchange=e=>{const file=e.target.files[0];e.target.value='';if(file)loadImage(file);};
$('color').onclick=()=>sample('../dist/assets/sample.jpg','sample.jpg');
$('gray').onclick=()=>sample('./assets/gray-nest.png','gray-nest.png');
$('cancel').onclick=()=>{stop();status('已取消。可以重新选择图片。');};
$('inference').onchange=()=>{if(packet)run('exportNetwork',{packet,view:{includeInference:$('inference').checked}});};
$('download').onclick=()=>{if(!packet)return;const url=URL.createObjectURL(new Blob([JSON.stringify(packet.network,null,2)],{type:'application/json'}));const a=document.createElement('a');a.href=url;a.download='Path_Recognize.network.json';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);};
busy(false);

window.addEventListener('path-recognize:language',()=>{status(lastStatus.key,lastStatus.params);if(!packet)$('summary').textContent=t('等待 network…');});
initI18n();status(lastStatus.key);$('summary').textContent=t('等待 network…');
