"use strict";
const _ = require("nzlib");
module.exports = class Slots {
	constructor(x) {
		this.x = x;
	}
	static main(){
		const obj = new Slots(1);
		const gotX = obj.getX();
		if (gotX !== 1)
			throw new _.Assertion_Exception();
		
	}
	getX(){
		return this.x;
	}
};
