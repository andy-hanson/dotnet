"use strict";
const _ = require("nzlib");
module.exports = class Literals {
	static main(){
		Literals.needs_nat(0);
		Literals.needs_int(0);
		Literals.needs_real(0);
		Literals.needs_nat(_.Int.to_nat(0));
		Literals.needs_int(0);
		Literals.needs_real(0);
		Literals.needs_nat(_.Int.to_nat(_.Real.round_down(0)));
		Literals.needs_int(_.Real.round_up(0));
		return Literals.needs_real(0);
	}
	static needs_nat(n) {}
	static needs_int(i) {}
	static needs_real(f) {}
};
