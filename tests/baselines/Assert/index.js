"use strict";
const _ = require("nzlib");
module.exports = class Assert {
	static main(){
		if (!(true))
			throw new _.Assertion_Exception();
		
	}
};
