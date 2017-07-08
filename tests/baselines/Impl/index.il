constructor
	this
	get parameter 0
	set instance field x
	return

Void main() [PrivateScope, Public, Static]

Int n() [PrivateScope, Public, Final, Virtual]

n
	this
	get instance field Impl.x
	return

main
	const 1
	call non-virtual Int.of
	new Impl
	declare local instance
	set local instance
	get local instance
	call non-virtual AbstractClass.getN
	declare local gotN
	set local gotN
	get local gotN
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

