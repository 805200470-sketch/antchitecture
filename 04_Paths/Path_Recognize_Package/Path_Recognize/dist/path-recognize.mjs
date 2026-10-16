/** Public, framework-independent integration API. No DOM, model, network or build step. */
import {extractPaths,analyzeMask,validateROI,ENGINE_VERSION} from './engine.mjs';
import {annotateEvidence} from './reconstruction.mjs';
import {centerRoutes} from './center-routes.mjs';
import {selectMainPaths} from './main-paths.mjs';
import {smoothGraph} from './path-smoothing.mjs';
import {serializeNetwork} from './network-export.mjs';
export {ENGINE_VERSION};
export const DEFAULT_OPTIONS=Object.freeze({sensitivity:50,contrast:1.3,denoise:2,minArea:50,detectSurface:true,closeGaps:false,reconstruct:true,maxGap:42});
const numeric={sensitivity:[0,100],contrast:[1,2],denoise:[0,3],minArea:[0,10000000],maxGap:[4,100]};
function dimensions(width,height){if(!Number.isInteger(width)||!Number.isInteger(height)||width<1||height<1||Math.max(width,height)>1400)throw new RangeError('Processing dimensions must be integers in 1..1400. Resize the image before recognition.');}
function viewOptions(context,view={}){
 const v={pathMode:context.pathMode??'main',smoothing:context.smoothing??1,includeInference:context.includeInference??true,...view};
 if(!['main','full'].includes(v.pathMode)||!Number.isInteger(v.smoothing)||v.smoothing<0||v.smoothing>3||typeof v.includeInference!=='boolean')throw new TypeError('Expected pathMode main/full, smoothing 0..3, and boolean includeInference.');
 return v;
}
function normalize(input){
 const {width,height,rgba}=input;dimensions(width,height);
 if(!(rgba instanceof Uint8Array||rgba instanceof Uint8ClampedArray)||rgba.length!==width*height*4)throw new TypeError('rgba must be a Uint8Array or Uint8ClampedArray with width*height*4 bytes.');
 const parameters={...DEFAULT_OPTIONS,...input.options};
 for(const [key,value]of Object.entries(parameters)){
  if(!(key in DEFAULT_OPTIONS))throw new TypeError('Unknown recognition option: '+key);
  const range=numeric[key];if(range?(!Number.isFinite(value)||value<range[0]||value>range[1]):typeof value!=='boolean')throw new RangeError('Invalid recognition option: '+key);
  if(['denoise','minArea','maxGap'].includes(key)&&!Number.isInteger(value))throw new TypeError(key+' must be an integer');
 }
 const originalWidth=input.image?.width??width,originalHeight=input.image?.height??height;
 if(!Number.isInteger(originalWidth)||!Number.isInteger(originalHeight)||originalWidth<1||originalHeight<1||originalWidth*originalHeight>60000000)throw new RangeError('Invalid original image dimensions (maximum 60 million pixels).');
 const name=input.image?.name??'image';if(typeof name!=='string')throw new TypeError('image.name must be a string');
 const context={width,height,originalWidth,originalHeight,name,roi:validateROI(input.roi??{x:0,y:0,width,height},width,height),parameters,manualEdits:0,...viewOptions(input)};
 return context;
}
function select(analysis,context,view){
 const {width,height,roi}=context;let result=analysis;
 if(!view.includeInference){
  const mask=analysis.mask.map((v,i)=>analysis.inferredMask[i]?0:v);
  result={...analysis,...analyzeMask(mask,width,height,roi),inferredMask:new Uint8Array(mask.length),bridges:[]};
  result.centerGraph=centerRoutes(result,width,height);
 }
 const graph=view.pathMode==='main'?(result.centerGraph??centerRoutes(result,width,height)):selectMainPaths(result,width,height,'full');
 return {result,graph:smoothGraph({...result,graph},width,height,view.smoothing)};
}
/** Re-export an existing analysis without rerunning segmentation. Does not mutate it. */
export function exportNetwork(analysis,context,view={}){
 dimensions(context.width,context.height);const v=viewOptions(context,view),{result,graph}=select(analysis,context,v);
 return serializeNetwork(result,graph,context,v.includeInference);
}
/** Synchronous CPU operation: call in integration-worker.mjs for browser production use. */
export function recognize(input){
 const context=normalize(input);
 // Match the page's white-background handling of transparent PNG pixels.
 const rgba=input.rgba.slice();for(let p=0;p<rgba.length;p+=4){const a=rgba[p+3]/255;for(let c=0;c<3;c++)rgba[p+c]=Math.round(rgba[p+c]*a+255*(1-a));rgba[p+3]=255;}
 const analysis=extractPaths({rgba,width:context.width,height:context.height,roi:context.roi,options:context.parameters});
 analysis.centerGraph=centerRoutes(analysis,context.width,context.height);
 return {analysis,context,network:exportNetwork(analysis,context)};
}
/** Recompute after a manual mask edit. No automatic gap reconnection or surface regrowth. */
export function updateMask(packet,mask){
 const {analysis:prior,context:previous}=packet;dimensions(previous.width,previous.height);
 if(!(mask instanceof Uint8Array)||mask.length!==previous.width*previous.height||mask.some(v=>v!==0&&v!==1))throw new TypeError('mask must be a binary Uint8Array matching the processing dimensions.');
 const context={...previous,manualEdits:previous.manualEdits+1};
 const analysis={...prior,...annotateEvidence(analyzeMask(mask,context.width,context.height,context.roi),prior.inferredMask,prior.bridges,context.width)};
 analysis.centerGraph=centerRoutes(analysis,context.width,context.height);
 return {analysis,context,network:exportNetwork(analysis,context)};
}
