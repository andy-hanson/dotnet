/** @typedef {number} nat */
/** @typedef {number} int */
/** @typedef {number} real */

Object.assign(exports, require("./primitive")); // `export * from "./primitive";`

/**
 * @abstract
 */
class Exception extends Error {
	constructor() {
		super();
		this.message = this.description();
	}

	/**
	 * @return {string}
	 */
	description() { throw notImplemented(); }
}
exports.Exception = Exception;

class AssertionException extends Exception {
	description() {
		return "Assertion failed.";
	}
}
exports.AssertionException = AssertionException;

class Console extends null {
	write_line() { throw notImplemented(); }
}
exports.Console = Console;

exports.mixin = require("./mixin");

function notImplemented() {
	throw new Error("Not implemented.");
}
