function Slots(x) {
	this.x = x;
}
Slots.main = function (x) {
	var obj = new Slots(x);
	return obj.getX();
};
Slots.prototype.getX = function (){
	return this.x;
};
module.exports = Slots;
