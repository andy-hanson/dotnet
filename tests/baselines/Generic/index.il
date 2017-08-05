T f<T>(T) [PrivateScope, Public, Static]

Void main() [PrivateScope, Public, Static]

f
	get parameter 0
	return

main
	const uint 0
	call non-virtual Nat.of
	call non-virtual Generic.f
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	return

