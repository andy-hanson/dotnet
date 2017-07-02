constructor
	this
	get parameter 0
	set instance field x
	return

impl n
	this
	get instance field Impl.x
	return

fun main
	const 1
	call static Int.of
	new Impl
	declare local instance
	set local instance
	get local instance
	call static AbstractClass.getN
	declare local gotN
	set local gotN
	get local gotN
	const 2
	call static Int.of
	call instance Int._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

