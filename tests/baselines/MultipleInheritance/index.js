const _ = require("nzlib");
const Super1 = require("./Super1");
const Super2 = require("./Super2");
module.exports = class MultipleInheritance extends _.mixin(Super1, Super2) {
	constructor(x) {
		super();
		this.x = x;
	}
	s1(){
		return this.x;
	}
	s2(){
		return this.x + 1;
	}
	static main(){
		const x = new MultipleInheritance(1);
		if (!(x.s1Def() === 10)) throw new _.AssertionException();
		if (!(x.s2Def() === 20)) throw new _.AssertionException();
	}
};
