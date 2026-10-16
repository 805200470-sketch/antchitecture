import {recognize,updateMask,exportNetwork} from './path-recognize.mjs';
// One request -> one response. Caller owns IDs and may terminate this worker to cancel.
self.onmessage=({data})=>{
 const {id,kind}=data;
 try{
  let result;
  if(kind==='recognize')result=recognize(data.input);
  else if(kind==='updateMask')result=updateMask(data.packet,data.mask);
  else if(kind==='exportNetwork')result=exportNetwork(data.packet.analysis,data.packet.context,data.view);
  else throw new TypeError('Unknown operation: '+kind);
  const transfers=result.analysis?[...new Set(Object.values(result.analysis).filter(ArrayBuffer.isView).map(a=>a.buffer))]:[];
  self.postMessage({id,ok:true,result},transfers);
 }catch(error){self.postMessage({id,ok:false,error:{name:error.name,message:error.message}});}
};
