import {extractPaths,analyzeMask} from './engine.mjs';
import {annotateEvidence} from './reconstruction.mjs';
import {centerRoutes} from './center-routes.mjs';
self.onmessage = ({data}) => {
 const {id,kind,width,height,roi}=data;
 try {
  const started=performance.now();
  const result=kind==='extract'?extractPaths(data):annotateEvidence(analyzeMask(data.mask,width,height,roi),data.inferredMask,data.bridges,width);
  result.centerGraph=centerRoutes(result,width,height);
  const transfers=[...new Set(['mask','skeleton','enhanced','depth','relief','ridge','inferredMask','directMask','surfaceCue'].filter(k=>result[k]).map(k=>result[k].buffer))];
  self.postMessage({id,result,elapsed:performance.now()-started},transfers);
 }catch(error){self.postMessage({id,error:error.message||'处理失败，请调整区域重试'});}
};
