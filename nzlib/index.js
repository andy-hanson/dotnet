const assert = require("assert"); //TODO: use noze-specific exceptions

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

class Assertion_Exception extends Exception {
	description() {
		return "Assertion failed.";
	}
}
exports.Assertion_Exception = Assertion_Exception;

class Console_App extends null {
	stdin() { throw notImplemented(); }
	stdout() { throw notImplemented(); }
	stderr() { throw notImplemented(); }
	installation_directory() { throw notImplemented(); }
	current_working_directory() { throw notImplemented(); }
}
exports.Console_App = Console_App;

class Read_Stream extends null {
	read_all() { throw notImplemented(); }
	write_all_to() { throw notImplemented(); }
	close() { throw notImplemented(); }
}
exports.Read_Stream = Read_Stream;

class Write_Stream extends null {
	write_all() { throw notImplemented(); }
	write() { throw notImplemented(); }
	write_line() { throw notImplemented(); }
	close() { throw notImplemented(); }
}
exports.Write_Stream = Write_Stream;

class File_System extends null {
	read() { throw notImplemented(); }
	write() { throw notImplemented(); }
	open_read() { throw notImplemented(); }
	open_write() { throw notImplemented(); }
}
exports.File_System = File_System;

class Path extends null {
	/** @param {ReadonlyArray<string>} parts */
	constructor(parts) {
		/**
		 * @readonly
		 * @type {ReadonlyArray<string>}
		 */
		this.parts = parts;
	}

	/**
	 * @param {string} pathString
	 * @return {Path}
	 */
	static from_string(pathString) {
		const parts = pathString.split('/');
		for (const p of parts)
			assert(isPathPart(p));
		return new Path(parts);
	}

	/**
	 * @return {Path}
	 */
	directory() {
		return new Path(this.parts.slice(0, this.parts.length - 1));
	}

	/**
	 * @param {string} childName
	 * @return {Path}
	 */
	child(childName) {
		assert(isPathPart(childName));
		return new Path([...this.parts, childName]);
	}

	/**
	 * @return {string}
	 */
	to_string() {
		return this.parts.join("/");
	}
}
exports.Path = Path;

const slash = "/".charCodeAt(0);
const backslash = "\\".charCodeAt(0);
/**
 * @param {string} s
 * @return {boolean}
 */
function isPathPart(s) {
	if (s.length == 0)
		return false;
	for (let i = 0; i < s.length; i++)
		switch (s.charCodeAt(i)) {
			case slash:
			case backslash:
				return false;
		}
	return true;
}

exports.mixin = require("./mixin");

function notImplemented() {
	throw new Error("Not implemented.");
}
