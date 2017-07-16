constructor
	this
	get parameter 0
	set instance field x
	return

Void main() [PrivateScope, Public, Static]

Nat s1() [PrivateScope, Public, Final, Virtual]

s1
	this
	get instance field Multiple-Inheritance.x
	return

Nat s2() [PrivateScope, Public, Final, Virtual]

s2
	this
	get instance field Multiple-Inheritance.x
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._add
	return

main
	const uint 1
	call non-virtual Nat.of
	new Multiple-Inheritance
	declare local x
	set local x
	get local x
	call non-virtual Super1.s1Def
	const uint 10
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	get local x
	call non-virtual Super2.s2Def
	const uint 20
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	return

