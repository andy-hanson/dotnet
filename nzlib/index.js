/** @typedef {number} nat */
/** @typedef {number} int */
/** @typedef {number} real */

class Exception extends Error {
	constructor() {
		super();
		// @ts-ignore (need abstract methods)
		this.message = this.description();
	}
}
exports.Exception = Exception;

class AssertionException extends Exception {
	description() {
		return "Assertion failed.";
	}
}
exports.AssertionException = AssertionException;

/**
 * @param {nat | int} a
 * @param {nat | int} b
 * @return {nat | int}
 */
exports.divInt = (a, b) => {
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

/**
 * @param {string} s
 * @return {int}
 */
exports.parseInt = s => { throw new Error("TODO"); }

/**
 * @param {string} s
 * @return {real}
 */
exports.parseReal = s => { throw new Error("TODO"); }

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

/**
 * @param {Function[]} classes
 * @return {Function}
*/
exports.mixin = (...classes) => {
	const protos = classes.map(cls => cls.prototype);
	function mixed() {}
	mixed.prototype = mixProto(protos);
	return mixed;
};

/**
 * @param {Object[]} protos
 * @return {Object}
 */
function mixProto(protos) {
	const t = { cache: null, protos };
	const p = new Proxy(t, protoProxyHandler);
	const cache = Object.create(p);
	t.cache = cache;
	return cache;
}

/** @type {ProxyHandler<Object>} */
const protoProxyHandler = {
	get(target, name) {
		const { cache, protos } = target;
		for (const proto of protos) {
			const got = proto[name];
			if (got !== undefined) {
				//Object.defineProperty(cache, name, { configurable: false, enumerable: false, writable: false, value: got });
				cache[name] = got;
				return got;
			}
		}
		throw new Error(`No such method ${JSON.stringify(name)}`);
	},
};
