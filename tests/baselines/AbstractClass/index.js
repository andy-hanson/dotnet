const _ = require("nzlib");
module.exports = class AbstractClass {
	static main(a) {
		if (!(a.s() === "s")) throw new _.AssertionException();
	}
};
