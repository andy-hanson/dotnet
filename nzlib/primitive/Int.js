/** @typedef {number} nat */
/** @typedef {number} int */

/**
 * @param {string} _s
 * @return {int}
 */
exports.parse = _s => { throw new Error("TODO"); }

/**
 * @param {int} a
 * @param {int} b
 * @return {int}
 */
exports._div = (a, b) => {
	return ((a | 0) / (b | 0)) | 0;
}

/**
 * @param {int} n
 * @return {nat}
 */
exports.toNat = (n) => {
	if (n < 0) {
		throw new Error("TODO");
	}
	return n;
}
