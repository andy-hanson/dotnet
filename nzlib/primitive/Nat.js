/** @typedef {number} nat */
/** @typedef {number} int */

/**
 * @param {nat} a
 * @param {nat} b
 * @return {nat}
 */
exports._div = (a, b) => ((a | 0) / (b | 0)) | 0;

/**
 * @param {nat} n
 * @return {nat}
 */
exports.decr = n => {
	if (n === 0)
		throw new Error("Decr fail"); //TODO: AssertionException
	return n - 1;
}
