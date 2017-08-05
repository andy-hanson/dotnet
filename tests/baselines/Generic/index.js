"use strict";
const _ = require("nzlib");
module.exports = class Generic {
	static f(t) {
		return t;
	}
	static main() {
		if (Generic.f(0) !== 0)
			throw new _.Assertion_Exception();
		
	}
};
