"use strict";
const _ = require("nzlib");
module.exports = class Operator_Parsing {
	static main(){
		const x = (1 + 2) * 3;
		if (x !== 9)
			throw new _.Assertion_Exception();
		
		const y = Operator_Parsing.double(x) + 1;
		if (y !== 19)
			throw new _.Assertion_Exception();
		
		const z = Operator_Parsing.double(x + 1);
		if (z !== 20)
			throw new _.Assertion_Exception();
		
	}
	static double(x) {
		return x * 2;
	}
};
