constructor
	this
	get parameter 0
	set instance field x
	return

Void main() [PrivateScope, Public, Static]

Nat getX() [PrivateScope, Public, Final]

main
	const uint 1
	call non-virtual Nat.of
	new Slots
	declare local obj
	set local obj
	get local obj
	call non-virtual Slots.getX
	declare local gotX
	set local gotX
	get local gotX
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	return

getX
	this
	get instance field Slots.x
	return

