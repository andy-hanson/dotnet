"use strict";
const _ = require("nzlib");
const AbstractClass = require("./AbstractClass");
module.exports = class Impl extends AbstractClass {
	constructor(x) {
		super();
		this.x = x;
	}
	n(){
		return this.x;
	}
	static main(){
		const instance = new Impl(1);
		const gotN = AbstractClass.getN(instance);
		if (gotN !== 1)
			throw new _.AssertionException();
		
	}
};
