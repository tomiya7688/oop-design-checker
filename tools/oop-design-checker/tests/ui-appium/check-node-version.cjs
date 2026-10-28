// CVE-2026-111497: each pair is the fixed minor and patch for that major.
const MINIMUM_PATCH_BY_MAJOR = new Map([
  [22, [23, 4]],
  [24, [22, 1]],
]);

function isSupportedNodeVersion(version, requiredMajor) {
  const match = /^(\d+)\.(\d+)\.(\d+)$/.exec(version);
  const minimum = MINIMUM_PATCH_BY_MAJOR.get(requiredMajor);
  if (!match || !minimum) return false;
  const [major, minor, patch] = match.slice(1).map(Number);
  if (major !== requiredMajor) return false;
  return minor > minimum[0] || (minor === minimum[0] && patch >= minimum[1]);
}

if (require.main === module) {
  const version = process.versions.node;
  const requested = process.argv[2];
  const major = /^(22|24)$/.test(requested || '') ? Number(requested) : NaN;
  const minimum = MINIMUM_PATCH_BY_MAJOR.get(major);
  console.log(`Node.js ${version}`);
  if (!isSupportedNodeVersion(version, major)) {
    console.error(
      minimum
        ? `Node.js ${major}.${minimum[0]}.${minimum[1]} or later in the ${major}.x line is required.`
        : 'Specify the Node.js major version 22 or 24.',
    );
    process.exitCode = 1;
  }
}

module.exports = { isSupportedNodeVersion };
