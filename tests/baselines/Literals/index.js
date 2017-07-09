"use strict";
const _ = require("nzlib");
module.exports = class Literals {
	static main(){
		Literals.needsNat(0);
		Literals.needsInt(0);
		Literals.needsReal(0);
		Literals.needsNat(_.toNat(0));
		Literals.needsInt(0);
		Literals.needsReal(0);
		Literals.needsNat(_.toNat(_.roundDown(0)));
		Literals.needsInt(_.roundUp(0));
		return Literals.needsReal(0);
	}
	static needsNat(n) {}
	static needsInt(i) {}
	static needsReal(f) {}
};
