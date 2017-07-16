"use strict";
const _ = require("nzlib");
module.exports = class Abstract_Class {
	static main(a) {
		if (a.s() !== "s")
			throw new _.Assertion_Exception();
		
	}
};
