Void main() [PrivateScope, Public, Static]

Nat double(Nat) [PrivateScope, Public, Static]

Nat add(Nat, Nat) [PrivateScope, Public, Static]

Nat mul(Nat, Nat) [PrivateScope, Public, Static]

main
	const uint 1
	call non-virtual Nat.of
	const uint 2
	call non-virtual Nat.of
	call non-virtual Nat._add
	const uint 3
	call non-virtual Nat.of
	call non-virtual Nat._mul
	declare local a
	set local a
	get local a
	const uint 9
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	get local a
	call non-virtual Operator-Parsing.double
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._add
	declare local b
	set local b
	get local b
	const uint 19
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	get local a
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._add
	call non-virtual Operator-Parsing.double
	declare local c
	set local c
	get local c
	const uint 20
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	const uint 1
	call non-virtual Nat.of
	call non-virtual Operator-Parsing.double
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._add
	const uint 2
	call non-virtual Nat.of
	const uint 2
	call non-virtual Nat.of
	call non-virtual Nat._mul
	call non-virtual Operator-Parsing.add
	declare local d
	set local d
	get local d
	const uint 7
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	get local a
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._add
	call non-virtual Operator-Parsing.double
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._add
	declare local e
	set local e
	get local e
	const uint 21
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	const uint 1
	call non-virtual Nat.of
	const uint 2
	call non-virtual Nat.of
	const uint 3
	call non-virtual Nat.of
	call non-virtual Operator-Parsing.mul
	call non-virtual Operator-Parsing.add
	declare local f
	set local f
	get local f
	const uint 7
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
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

add
	get parameter 0
	get parameter 1
	call non-virtual Nat._add
	return

mul
	get parameter 0
	get parameter 1
	call non-virtual Nat._mul
	return

