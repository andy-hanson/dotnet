var nzlib = {};

var Exception = nzlib.Exception = function() {
	this.error = new Error();
}

var AssertionException = nzlib.AssertionException = function() {
	Exception.call(this);
}
AssertionException.prototype = Object.create(Exception.prototype);
AssertionException.prototype.description = function() {
	return "Assertion failed.";
}

module.exports = nzlib;
