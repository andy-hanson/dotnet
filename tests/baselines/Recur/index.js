"use strict";
const _ = require("nzlib");
module.exports = class Recur {
	static main(){
		const x = Recur.f(10);
		if (!(x === 0))
			throw new _.AssertionException();
		
	}
	static f(i) {
		if (i === 0)
			return 0;
		else {
			const m = i - 1;
			return Recur.f(m);
		}
	}
};
