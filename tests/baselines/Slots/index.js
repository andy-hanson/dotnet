var _ = require("nzlib");
module.exports = class Slots {
	constructor(x) {
		this.x = x;
	}
	static main(){
		var obj = new Slots(1);
		var gotX = obj.getX();
		if (!(gotX === 1)) throw new _.AssertionException();
	}
	getX(){
		return this.x;
	}
};
