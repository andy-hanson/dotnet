/** @typedef {number} nat */
/** @typedef {number} int */

/**
 * @param {nat} a
 * @param {nat} b
 * @return {nat}
 */
exports._div = (a, b) => {
	return ((a | 0) / (b | 0)) | 0;
}
