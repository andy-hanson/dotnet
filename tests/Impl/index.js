var AbstractClass = require("RelPath");
function Impl(x) {
	this.x = x;
}
Impl.prototype = Object.create(AbstractClass.prototype);
Impl.main = function (x) {
	var instance = new Impl(x);return AbstractClass.getN(instance);
};
module.exports = Impl;
