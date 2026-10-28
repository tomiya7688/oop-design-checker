const { test } = require('node:test');
const assert = require('node:assert/strict');
const { spawnSync } = require('node:child_process');
const path = require('node:path');
const { isSupportedNodeVersion } = require('./check-node-version.cjs');

test('accepts fixed and newer releases in the requested line', () => {
  for (const [version, major] of [
    ['24.22.1', 24],
    ['24.22.2', 24],
    ['24.23.0', 24],
    ['22.23.4', 22],
    ['22.24.0', 22],
  ])
    assert.equal(isSupportedNodeVersion(version, major), true, version);
});

test('rejects vulnerable, wrong-line and prerelease versions', () => {
  for (const [version, major] of [
    ['24.20.0', 24],
    ['24.22.0', 24],
    ['22.23.3', 22],
    ['22.23.4', 24],
    ['24.22.1', 22],
    ['25.0.0', 25],
    ['24.22.1-rc.1', 24],
    ['24.22', 24],
    ['invalid', 24],
  ])
    assert.equal(isSupportedNodeVersion(version, major), false, version);
});

test('CLI rejects missing and unsupported major arguments', () => {
  const script = path.join(__dirname, 'check-node-version.cjs');
  for (const args of [[], ['23'], ['022']]) {
    const result = spawnSync(process.execPath, [script, ...args], {
      encoding: 'utf8',
    });
    assert.equal(result.status, 1, `arguments: ${args}`);
  }
});

test('CLI exits according to the actual runtime version', () => {
  const script = path.join(__dirname, 'check-node-version.cjs');
  const result = spawnSync(process.execPath, [script, '24'], {
    encoding: 'utf8',
  });
  assert.equal(
    result.status,
    isSupportedNodeVersion(process.versions.node, 24) ? 0 : 1,
  );
});
