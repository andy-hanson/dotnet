class Exception extends Error {
	constructor() {
		super();
		this.message = this.description();
	}
}

class AssertionException extends Exception {
	description() {
		return "Assertion failed.";
	}
}

module.exports = { Exception, AssertionException };
