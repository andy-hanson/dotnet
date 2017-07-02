var _ = require("nzlib");
var AbstractClass = require("./AbstractClass");
function Impl(x) {
	this.x = x;
}
Impl.prototype = Object.create(AbstractClass.prototype);
Impl.prototype.n = function (){
	return this.x;
};
Impl.main = function (){
	var instance = new Impl(1);
	var gotN = AbstractClass.getN(instance);
	if (!(gotN === 2)) throw new _.AssertionException();
};
module.exports = Impl;
