Void main() [PrivateScope, Public, Static]

Nat f(Bool) [PrivateScope, Public, Static]

main
	load static field Bool.boolTrue
	call non-virtual If-Else.f
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
	load static field Bool.boolFalse
	call non-virtual If-Else.f
	const uint 2
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	return

f
	get parameter 0
	get instance field Bool.value
	goto if false: else
	const uint 1
	call non-virtual Nat.of
	goto endIf
	else:
	const uint 2
	call non-virtual Nat.of
	endIf:
	return

