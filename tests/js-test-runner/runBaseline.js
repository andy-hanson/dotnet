// @ts-check

const { existsSync } = require("fs");
const { join } = require("path");

const testDir = join(__dirname, "..");

/**
 * @param {"normal" | "special"} kind
 * @param {string} testPath Test path relative to tests/cases
 * @return {void}
 */
function runBaseline(kind, testPath) {
	const testedClass = require(join(testDir, "baselines", testPath, "index.js"));

	/** @type {{}} */
	const result = kind === "special" ? require(testScriptPath(testPath))(testedClass) : testedClass.main();
	if (result !== undefined)
		throw new Error("Test result must be of type 'void'");
}

/**
 * @param {string} testPath
 * @param {*} testPath
 */
function runBaselineAndInferKind(testPath) {
	const kind = existsSync(testScriptPath(testPath)) ? "special" : "normal";
	runBaseline(kind, testPath);
}

/**
 * @param {string} testPath
 * @return {string}
 */
function testScriptPath(testPath) {
	return join(testDir, "cases", testPath, "test.js")
}

module.exports = { runBaseline, runBaselineAndInferKind };
