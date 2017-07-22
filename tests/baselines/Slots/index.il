constructor
	this
	get parameter 0
	set instance field Slots.x
	this
	get parameter 1
	set instance field Slots.y
	return

Void main() [PrivateScope, Public, Static]

Void incrY() [PrivateScope, Public, Final]

main
	const uint 1
	call non-virtual Nat.of
	const uint 2
	call non-virtual Nat.of
	new Slots
	declare local obj
	set local obj
	get local obj
	get instance field Slots.x
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	get local obj
	get instance field Slots.y
	const uint 2
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	get local obj
	call non-virtual Slots.incrY
	pop
	get local obj
	get instance field Slots.y
	const uint 3
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	return

incrY
	this
	this
	get instance field Slots.y
	call non-virtual Nat.incr
	set instance field Slots.y
	load static field Void.instance
	return

