// @ts-check

const { existsSync } = require("fs");
const { join } = require("path");

/**
 * @param {string} name
 * @return {string | undefined}
 */
function runBaseline(name) {
	const testDir = join(__dirname, "..");

	const testedClass = require(join(testDir, "baselines", name, "index.js"));

	const testScriptPath = join(testDir, "cases", name, "test.js");
	/** @type {function(any): {}} */
	const testScript = existsSync(testScriptPath) ? require(testScriptPath) : cls => cls.main();

	/** @type {{}} */
	const result = testScript(testedClass);
	if (result !== undefined && typeof result !== "string")
		throw new Error("Test result must be string or undefined");
	return result;
}

module.exports = runBaseline;
