constructor
	this
	get parameter 0
	set instance field x
	return

fun main
	const 1
	call static Int.of
	new Slots
	declare local obj
	set local obj
	get local obj
	call instance Slots.getX
	declare local gotX
	set local gotX
	get local gotX
	const 1
	call static Int.of
	call instance Int._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

def getX
	this
	get instance field Slots.x
	return

