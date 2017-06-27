function AbstractClass(){}
AbstractClass.getN = function (a) {
	return a.n();
};
module.exports = AbstractClass;
