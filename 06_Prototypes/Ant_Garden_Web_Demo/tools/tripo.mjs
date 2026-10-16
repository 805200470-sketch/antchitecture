#!/usr/bin/env node
// Tripo (v3 OpenAPI) image → textured 3D model, downloaded as GLB.
// The API key is read from .secrets/tripo_api_key.txt (or TRIPO_API_KEY) and never printed.
//
//   node tools/tripo.mjs balance
//   node tools/tripo.mjs image <front.png> <out.glb> [--model P1-20260311] [--faces 20000] [--quality standard|detailed|extreme]
//
// Every run appends a line to tripo_log.jsonl next to the output model (task id, model, file, credits consumed).
// API reference: https://developers.tripo3d.ai/en/docs/generation-image-to-model/p.md
import { readFileSync, writeFileSync, appendFileSync, existsSync } from 'node:fs';
import { basename, extname, join, resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const BASE = 'https://openapi.tripo3d.ai/v3';

function apiKey() {
  const file = join(ROOT, '.secrets/tripo_api_key.txt');
  const key = (process.env.TRIPO_API_KEY ?? (existsSync(file) ? readFileSync(file, 'utf8') : '')).trim();
  if (!key) {
    console.error('No Tripo API key. Paste it (one line) into .secrets/tripo_api_key.txt');
    process.exit(2);
  }
  return key;
}

async function call(method, path, body, isForm = false) {
  const res = await fetch(`${BASE}${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${apiKey()}`,
      ...(body && !isForm ? { 'Content-Type': 'application/json' } : {}),
    },
    body: body ? (isForm ? body : JSON.stringify(body)) : undefined,
  });
  const text = await res.text();
  let json;
  try {
    json = JSON.parse(text);
  } catch {
    throw new Error(`${method} ${path} → HTTP ${res.status}: ${text.slice(0, 300)}`);
  }
  if (!res.ok || (json.code !== undefined && json.code !== 0)) {
    throw new Error(`${method} ${path} → HTTP ${res.status}: ${JSON.stringify(json).slice(0, 400)}`);
  }
  return json.data ?? json;
}

async function upload(path) {
  const mime = { '.png': 'image/png', '.jpg': 'image/jpeg', '.jpeg': 'image/jpeg', '.webp': 'image/webp' }[extname(path).toLowerCase()];
  const form = new FormData();
  form.append('file', new Blob([readFileSync(path)], { type: mime ?? 'image/png' }), basename(path));
  const data = await call('POST', '/files', form, true);
  return data.file_token ?? data.image_token ?? data.token;
}

async function waitFor(taskId) {
  const t0 = Date.now();
  for (;;) {
    const data = await call('GET', `/tasks/${taskId}`);
    const status = data.status;
    process.stdout.write(`\r  ${status} ${data.progress ?? ''}%  ${((Date.now() - t0) / 1000).toFixed(0)}s   `);
    if (status === 'success') {
      process.stdout.write('\n');
      return data;
    }
    if (['failed', 'cancelled', 'banned', 'expired', 'unknown'].includes(status)) {
      throw new Error(`task ${taskId} ended with status ${status}: ${JSON.stringify(data).slice(0, 300)}`);
    }
    if (Date.now() - t0 > 15 * 60 * 1000) throw new Error(`task ${taskId} timed out`);
    await new Promise((r) => setTimeout(r, 4000));
  }
}

async function imageToModel(img, out, opts) {
  console.log(`upload ${img}`);
  const fileToken = await upload(img);
  // v3: the uploaded image goes in `input` (a file token, an image URL or an earlier image task id)
  const body = { input: fileToken, model: opts.model, face_limit: opts.faces, texture: true, pbr: true, texture_quality: opts.quality };
  const { task_id: taskId } = await call('POST', '/generation/image-to-model', body);
  console.log(`task ${taskId} (${opts.model})`);
  const data = await waitFor(taskId);
  const url = data.output?.model_url ?? data.output?.pbr_model ?? data.output?.model;
  if (!url) throw new Error(`no model url in ${JSON.stringify(data.output).slice(0, 300)}`);
  const res = await fetch(url); // signed URL, expires in minutes: download right away
  if (!res.ok) throw new Error(`download failed: HTTP ${res.status}`);
  writeFileSync(out, Buffer.from(await res.arrayBuffer()));
  const entry = { time: new Date().toISOString(), task: taskId, model: opts.model, faces: opts.faces, quality: opts.quality, image: img, out, credits: data.credits_consumed ?? data.consumed_credit ?? null };
  appendFileSync(join(dirname(out), 'tripo_log.jsonl'), JSON.stringify(entry) + '\n');
  console.log(`saved ${out}${entry.credits !== null ? `  (credits: ${entry.credits})` : ''}`);
}

const [cmd, ...rest] = process.argv.slice(2);
const flag = (name, def) => {
  const i = rest.indexOf(name);
  return i >= 0 ? rest[i + 1] : def;
};
try {
  if (cmd === 'balance') {
    console.log(JSON.stringify(await call('GET', '/account/balance'), null, 2));
  } else if (cmd === 'image') {
    const [img, out] = rest;
    if (!img || !out) throw new Error('usage: node tools/tripo.mjs image <image> <out.glb>');
    await imageToModel(resolve(img), resolve(out), { model: flag('--model', 'P1-20260311'), faces: Number(flag('--faces', 20000)), quality: flag('--quality', 'standard') });
  } else {
    console.log('usage: node tools/tripo.mjs balance | image <image> <out.glb> [--model P1-20260311] [--faces 20000] [--quality standard|detailed|extreme]');
  }
} catch (err) {
  console.error(`\n${err.message}`);
  process.exit(1);
}
