"use strict";
const _ = require("nzlib");
module.exports = class If_Else {
	static main() {
		if (If_Else.f(true) !== 1)
			throw new _.Assertion_Exception();
		
		if (If_Else.f(false) !== 2)
			throw new _.Assertion_Exception();
		
	}
	static f(b) {
		if (b)
			return 1;
		else
			return 2;
		
	}
};
