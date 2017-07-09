Void main() [PrivateScope, Public, Static]

Nat double(Nat) [PrivateScope, Public, Static]

main
	const uint 1
	call non-virtual Nat.of
	const uint 2
	call non-virtual Nat.of
	call non-virtual Nat._add
	const uint 3
	call non-virtual Nat.of
	call non-virtual Nat._mul
	declare local x
	set local x
	get local x
	const uint 9
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	pop
	get local x
	call non-virtual OperatorParsing.double
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._add
	declare local y
	set local y
	get local y
	const uint 19
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	pop
	get local x
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._add
	call non-virtual OperatorParsing.double
	declare local z
	set local z
	get local z
	const uint 20
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

double
	get parameter 0
	const uint 2
	call non-virtual Nat.of
	call non-virtual Nat._mul
	return

