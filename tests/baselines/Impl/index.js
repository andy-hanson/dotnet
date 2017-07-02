var AbstractClass = require("./AbstractClass");
function Impl(x) {
	this.x = x;
}
Impl.prototype = Object.create(AbstractClass.prototype);
Impl.prototype.n = function (){
	return this.x;
};
Impl.main = function (x) {
	var instance = new Impl(x);
	return AbstractClass.getN(instance);
};
module.exports = Impl;
