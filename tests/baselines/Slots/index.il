constructor
	this
	get parameter 0
	set instance field x
	return

Void main() [PrivateScope, Public, Static]

Int getX() [PrivateScope, Public, Final]

main
	const 1
	call non-virtual Int.of
	new Slots
	declare local obj
	set local obj
	get local obj
	call non-virtual Slots.getX
	declare local gotX
	set local gotX
	get local gotX
	const 1
	call non-virtual Int.of
	call non-virtual Int._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

getX
	this
	get instance field Slots.x
	return

