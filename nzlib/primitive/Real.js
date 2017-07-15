/** @typedef {number} int */
/** @typedef {number} real */

/**
 * @param {string} _s
 * @return {real}
 */
exports.parse = _s => { throw new Error("TODO"); }

/**
 * @param {real} r
 * @return {int}
 */
exports.round = r => Math.round(r);

/**
 * @param {real} r
 * @return {int}
 */
exports.roundDown = r => Math.floor(r);

/**
 * @param {real} r
 * @return {int}
 */
exports.roundUp = r => Math.ceil(r);
