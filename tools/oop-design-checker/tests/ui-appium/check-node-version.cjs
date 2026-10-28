const MINIMUM_NODE_VERSION = [24, 22, 1];

function isSupportedNodeVersion(version) {
  const match = /^(\d+)\.(\d+)\.(\d+)$/.exec(version);
  if (!match) return false;
  const [major, minor, patch] = match.slice(1).map(Number);
  if (major !== MINIMUM_NODE_VERSION[0]) return false;
  return (
    minor > MINIMUM_NODE_VERSION[1] ||
    (minor === MINIMUM_NODE_VERSION[1] && patch >= MINIMUM_NODE_VERSION[2])
  );
}

if (require.main === module) {
  const version = process.versions.node;
  console.log(`Node.js ${version}`);
  if (!isSupportedNodeVersion(version)) {
    console.error('Node.js 24.22.1 or later in the 24.x line is required.');
    process.exitCode = 1;
  }
}

module.exports = { isSupportedNodeVersion };
