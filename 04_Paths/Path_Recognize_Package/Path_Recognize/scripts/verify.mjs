import {readFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
const root=fileURLToPath(new URL('../',import.meta.url));
try {
 const manifest=await readFile(path.join(root,'MANIFEST.sha256'),'utf8');let count=0;
 for(const line of manifest.trim().split('\n')){
  const match=/^([a-f0-9]{64})  (.+)$/.exec(line);if(!match)throw new Error('Invalid manifest line');
  const [,expected,name]=match,file=path.resolve(root,name);
  if(path.relative(root,file).startsWith('..')||path.isAbsolute(name))throw new Error('Invalid manifest path');
  const actual=createHash('sha256').update(await readFile(file)).digest('hex');
  if(actual!==expected)throw new Error('File changed or corrupted: '+name);count++;
 }
 console.log(`PASS: ${count} packaged files match MANIFEST.sha256. Intentional edits will change these hashes.`);
}catch(error){console.error(error.message);process.exitCode=1;}
