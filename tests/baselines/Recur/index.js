"use strict";
const _ = require("nzlib");
module.exports = class Recur {
	static main(){
		if (Recur.f(10) !== 0)
			throw new _.AssertionException();
		
	}
	static f(i) {
		if (i === 0)
			return 0;
		else return Recur.f(i - 1);
	}
};
