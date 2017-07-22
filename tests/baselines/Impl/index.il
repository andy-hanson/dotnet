constructor
	this
	get parameter 0
	set instance field Impl.x
	return

Void main() [PrivateScope, Public, Static]

Nat n() [PrivateScope, Public, Final, Virtual]

n
	this
	get instance field Impl.x
	return

main
	const uint 1
	call non-virtual Nat.of
	new Impl
	declare local instance
	set local instance
	get local instance
	call non-virtual AbstractClass.getN
	declare local gotN
	set local gotN
	get local gotN
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

