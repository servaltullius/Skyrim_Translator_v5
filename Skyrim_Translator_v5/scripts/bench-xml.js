#!/usr/bin/env node
/* Benchmark: escapeXml throughput and allocations (approx) */
/* Requires: devDependency "ts-node". Run: npm run bench:xml */

process.on('unhandledRejection', (e) => {
  console.error(e);
  process.exit(1);
});

// Force production-like path (no NOOP gate)
const prevNODE = process.env.NODE_ENV;
const prevMODE = process.env.DL_XML_ESCAPE_MODE;
process.env.NODE_ENV = 'production';
delete process.env.DL_XML_ESCAPE_MODE;

// Register TS loader to import TS sources directly
require('ts-node/register/transpile-only');

const { performance } = require('perf_hooks');
const { escapeXml } = require('../src/core/utils/xml');

function mulberry32(a) {
  return function () {
    let t = (a += 0x6d2b79f5);
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function genStr(rnd, len, charset) {
  let s = '';
  for (let i = 0; i < len; i++) s += charset.charAt((rnd() * charset.length) | 0);
  return s;
}

function makeDataset(kind, count, seed = 12345) {
  const rnd = mulberry32(seed);
  const arr = [];
  if (kind === 'no-specials') {
    const cs = 'abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-{}%$';
    for (let i = 0; i < count; i++) arr.push(genStr(rnd, 128, cs));
  } else if (kind === 'mixed') {
    const cs = 'abcXYZ0123 <>"\'';
    for (let i = 0; i < count; i++) arr.push(genStr(rnd, 128, cs));
  } else if (kind === 'pre-escaped') {
    const base = 'Fish & Chips <tag> &#34; &#x27; ';
    for (let i = 0; i < count; i++) arr.push(base.repeat(8));
  } else if (kind === 'long-mixed') {
    const base = 'Lorem & ipsum <dolor> "sit" \'amet\' & ';
    for (let i = 0; i < count; i++) arr.push(base.repeat(200)); // ~10k chars
  }
  return arr;
}

function benchOnce(name, data) {
  const mem0 = process.memoryUsage().heapUsed;
  const t0 = performance.now();
  let bytes = 0;
  for (let i = 0; i < data.length; i++) {
    const out = escapeXml(data[i]);
    bytes += out.length;
  }
  const t1 = performance.now();
  const mem1 = process.memoryUsage().heapUsed;
  const ms = t1 - t0;
  const mbps = (bytes / (1024 * 1024)) / (ms / 1000);
  const memDeltaKB = (mem1 - mem0) / 1024;
  return { name, count: data.length, ms: ms.toFixed(2), mbps: mbps.toFixed(2), memKB: memDeltaKB.toFixed(1) };
}

function run() {
  const suites = [
    ['no-specials', 20000],
    ['mixed', 20000],
    ['pre-escaped', 5000],
    ['long-mixed', 400],
  ];

  console.log('escapeXml benchmark (production path) — units: ms, MB/s, ΔKB');
  const results = [];
  for (const [kind, n] of suites) {
    const data = makeDataset(kind, n);
    // Warmup
    benchOnce(kind + ' (warmup)', data);
    // Measure
    const r = benchOnce(kind, data);
    results.push(r);
  }
  console.table(results);

  // restore env
  process.env.NODE_ENV = prevNODE;
  if (prevMODE === undefined) delete process.env.DL_XML_ESCAPE_MODE;
  else process.env.DL_XML_ESCAPE_MODE = prevMODE;
}

run();