var _ = require("nzlib");
var AbstractClass = require("./AbstractClass");
module.exports = class Impl extends AbstractClass {
	constructor(x) {
		super();
		this.x = x;
	}
	n(){
		return this.x;
	}
	static main(){
		var instance = new Impl(1);
		var gotN = AbstractClass.getN(instance);
		if (!(gotN === 1)) throw new _.AssertionException();
	}
};
