"use strict";
const _ = require("nzlib");
module.exports = class Recur {
	static main(){
		if (Recur.f(10) !== 0)
			throw new _.Assertion_Exception();
		
	}
	static f(i) {
		if (i === 0)
			return 0;
		else return Recur.f(_.Int.to_nat(i - 1));
	}
};
