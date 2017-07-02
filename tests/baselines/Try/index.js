var _ = require("nzlib");
var Try = {};
Try.main = function (){
	try {
		if (!(false)) throw new _.AssertionException();
		return "";
	} catch (e) {
		if (!(e instanceof _.Exception)) throw e;
		return e.description();
	}
};
module.exports = Try;
