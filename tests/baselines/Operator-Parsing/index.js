"use strict";
const _ = require("nzlib");
module.exports = class Operator_Parsing {
	static main() {
		const a = (1 + 2) * 3;
		if (a !== 9)
			throw new _.Assertion_Exception();
		
		const b = Operator_Parsing.double(a) + 1;
		if (b !== 19)
			throw new _.Assertion_Exception();
		
		const c = Operator_Parsing.double(a + 1);
		if (c !== 20)
			throw new _.Assertion_Exception();
		
		const d = Operator_Parsing.add(Operator_Parsing.double(1) + 1, 2 * 2);
		if (d !== 7)
			throw new _.Assertion_Exception();
		
		const e = Operator_Parsing.double(a + 1) + 1;
		if (e !== 21)
			throw new _.Assertion_Exception();
		
		const f = Operator_Parsing.add(1, Operator_Parsing.mul(2, 3));
		if (f !== 7)
			throw new _.Assertion_Exception();
		
	}
	static double(x) {
		return x * 2;
	}
	static add(a, b) {
		return a + b;
	}
	static mul(a, b) {
		return a * b;
	}
};
