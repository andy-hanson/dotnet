var _ = require("nzlib");
module.exports = class Try {
	static main(){
		var x = (() => {
			try {
				if (!(false)) throw new _.AssertionException();
				return "unreachable";
			} catch (e) {
				if (!(e instanceof _.Exception)) throw e;
				return e.description();
			}
		})();
		if (!(x === "Assertion failed.")) throw new _.AssertionException();
	}
};
