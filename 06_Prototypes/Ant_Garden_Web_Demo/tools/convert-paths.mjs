#!/usr/bin/env node
// Runs Path Recognize (the user's own engine, unchanged) over images prepared by
// tools/prepare_paths.py and writes schemaVersion 2.5 network JSON for the demo.
//
// Usage: node tools/convert-paths.mjs <work_dir> <out_dir> [path-recognize.mjs]
import { readFileSync, writeFileSync, readdirSync, mkdirSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const [workDir, outDir, enginePath = `${process.env.HOME}/Desktop/AntGarden/04_Paths/Path_Recognize_Package/Path_Recognize/dist/path-recognize.mjs`] =
  process.argv.slice(2);
if (!workDir || !outDir) {
  console.error('usage: node tools/convert-paths.mjs <work_dir> <out_dir> [path-recognize.mjs]');
  process.exit(1);
}
const { recognize, ENGINE_VERSION } = await import(pathToFileURL(resolve(enginePath)).href);
mkdirSync(outDir, { recursive: true });

const index = [];
for (const meta of readdirSync(workDir).filter((f) => f.endsWith('.json')).sort()) {
  const info = JSON.parse(readFileSync(join(workDir, meta), 'utf8'));
  const rgba = new Uint8Array(readFileSync(join(workDir, meta.replace(/\.json$/, '.rgba'))));
  // Path Recognize caps originals at 60 MP; larger drawings are reported at an
  // integer fraction of their size and the factor is recorded alongside.
  let coordScale = 1;
  while ((info.originalWidth / coordScale) * (info.originalHeight / coordScale) > 60e6) coordScale++;
  const packet = recognize({
    rgba,
    width: info.width,
    height: info.height,
    image: {
      name: info.name,
      width: Math.round(info.originalWidth / coordScale),
      height: Math.round(info.originalHeight / coordScale),
    },
    pathMode: 'full', // the drawings are already clean centrelines: keep every branch
    smoothing: 1,
    includeInference: false,
    options: { detectSurface: false, reconstruct: false, closeGaps: true, denoise: 1, minArea: 200 },
  });
  const net = packet.network;
  net.sourceImage = { width: info.originalWidth, height: info.originalHeight, coordinateScale: coordScale };
  const id = info.name.replace(/\.png$/i, '');
  writeFileSync(join(outDir, `${id}.json`), JSON.stringify(net));
  const junctions = net.nodes.filter((n) => n.type === 'junction').length;
  index.push({ id, file: `${id}.json`, nodes: net.nodes.length, edges: net.edges.length, junctions });
  console.log(`${id}: ${net.edges.length} edges, ${net.nodes.length} nodes (${junctions} junctions)`);
}
writeFileSync(join(outDir, 'index.json'), JSON.stringify({ engineVersion: ENGINE_VERSION, paths: index }, null, 2));
