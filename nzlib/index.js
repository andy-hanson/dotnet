class Exception extends Error {
	constructor() {
		super();
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

exports.divInt = (a, b) => {
	return ((a | 0) / (b | 0)) | 0;
}

exports.parseInt = (s) => { throw new Error("TODO"); }
exports.parseFloat = (s) => { throw new Error("TODO"); }
