"use strict";
const _ = require("nzlib");
module.exports = class Slots {
	constructor(x, y) {
		this.x = x;
		this.y = y;
	}
	static main() {
		const obj = new Slots(1, 2);
		if (obj.x !== 1)
			throw new _.Assertion_Exception();
		
		if (obj.y !== 2)
			throw new _.Assertion_Exception();
		
		obj.incrY();
		if (obj.y !== 3)
			throw new _.Assertion_Exception();
		
	}
	incrY() {
		return this.y = this.y + 1;
	}
};
