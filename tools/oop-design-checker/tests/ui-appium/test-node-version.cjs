const { test } = require('node:test');
const assert = require('node:assert/strict');
const { isSupportedNodeVersion } = require('./check-node-version.cjs');

test('accepts fixed and newer 24.x releases', () => {
  for (const version of ['24.22.1', '24.22.2', '24.23.0'])
    assert.equal(isSupportedNodeVersion(version), true, version);
});

test('rejects vulnerable, unsupported and prerelease versions', () => {
  for (const version of [
    '24.20.0',
    '24.22.0',
    '22.23.4',
    '25.0.0',
    '24.22.1-rc.1',
    '24.22',
    'invalid',
  ])
    assert.equal(isSupportedNodeVersion(version), false, version);
});
