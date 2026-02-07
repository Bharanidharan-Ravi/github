#!/usr/bin/env node
const fs = require('fs');
const path = require('path');
const { execSync } = require('child_process');

const root = process.cwd();
const dirNames = new Set(['node_modules', '.pnpm', '.vite', 'dist']);
const fileNames = new Set(['pnpm-lock.yaml', 'package-lock.json']);
let removed = [];

function safeRm(p) {
  try {
    fs.rmSync(p, { recursive: true, force: true });
    removed.push(p);
  } catch (e) {
    console.error('Failed to remove', p, e.message);
  }
}

function walk(dir) {
  let entries;
  try {
    entries = fs.readdirSync(dir, { withFileTypes: true });
  } catch (e) {
    return; // ignore permission errors / removed meanwhile
  }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (dirNames.has(entry.name)) {
        safeRm(full);
        continue;
      }
      // avoid infinite recursion into node_modules we've just removed
      walk(full);
    } else if (entry.isFile()) {
      if (fileNames.has(entry.name)) {
        try {
          fs.unlinkSync(full);
          removed.push(full);
        } catch (e) {
          console.error('Failed to remove file', full, e.message);
        }
      }
    }
  }
}

console.log('Cleaning workspace from', root);
// Remove top-level matches first
for (const name of dirNames) {
  const p = path.join(root, name);
  if (fs.existsSync(p)) safeRm(p);
}
for (const name of fileNames) {
  const p = path.join(root, name);
  if (fs.existsSync(p)) {
    try { fs.unlinkSync(p); removed.push(p); } catch (e) { console.error('Failed to remove file', p, e.message); }
  }
}

// Walk workspace to find nested matches
walk(root);

console.log('\nRemoved items:');
for (const r of removed) console.log('-', r);

console.log('\nPruning pnpm store and clearing cache (may take a while)...');
try {
  execSync('pnpm store prune', { stdio: 'inherit' });
} catch (e) {
  console.error('pnpm store prune failed:', e.message);
}
try {
  execSync('pnpm cache clean --all', { stdio: 'inherit' });
} catch (e) {
  // older pnpm may not support --all
  try {
    execSync('pnpm cache clean', { stdio: 'inherit' });
  } catch (e2) {
    console.error('pnpm cache clean failed:', e2.message);
  }
}

console.log('\nDone. Run `pnpm install` to reinstall dependencies.');
