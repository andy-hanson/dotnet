"use strict";
const _ = require("nzlib");
module.exports = class OperatorParsing {
	static main(){
		const x = (1 + 2) * 3;
		if (x !== 9)
			throw new _.AssertionException();
		
		const y = OperatorParsing.double(x) + 1;
		if (y !== 19)
			throw new _.AssertionException();
		
		const z = OperatorParsing.double(x + 1);
		if (z !== 20)
			throw new _.AssertionException();
		
	}
	static double(x) {
		return x * 2;
	}
};
