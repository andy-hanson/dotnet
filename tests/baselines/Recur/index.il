Void main() [PrivateScope, Public, Static]

Nat factorial(Nat) [PrivateScope, Public, Static]

Nat factorial-recursive(Nat, Nat) [PrivateScope, Public, Static]

Nat fibonacci(Nat) [PrivateScope, Public, Static]

Nat fibonacci-recursive(Nat, Nat, Nat) [PrivateScope, Public, Static]

main
	const uint 5
	call non-virtual Nat.of
	call non-virtual Recur.factorial
	const uint 120
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	const uint 0
	call non-virtual Nat.of
	call non-virtual Recur.fibonacci
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	pop
	const uint 2
	call non-virtual Nat.of
	call non-virtual Recur.fibonacci
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
	const uint 6
	call non-virtual Nat.of
	call non-virtual Recur.fibonacci
	const uint 8
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	return

factorial
	get parameter 0
	const uint 1
	call non-virtual Nat.of
	call non-virtual Recur.factorial-recursive
	return

factorial-recursive
	get parameter 0
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if false: else
	get parameter 1
	goto endWhen
	else:
	get parameter 0
	call non-virtual Nat.decr
	get parameter 1
	get parameter 0
	call non-virtual Nat._mul
	tail call non-virtual Recur.factorial-recursive
	endWhen:
	return

fibonacci
	get parameter 0
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if false: case1
	const uint 0
	call non-virtual Nat.of
	goto endWhen
	case1:
	get parameter 0
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if false: else
	const uint 1
	call non-virtual Nat.of
	goto endWhen
	else:
	get parameter 0
	const uint 0
	call non-virtual Nat.of
	const uint 1
	call non-virtual Nat.of
	call non-virtual Recur.fibonacci-recursive
	endWhen:
	return

fibonacci-recursive
	get parameter 0
	const uint 2
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if false: else
	get parameter 1
	get parameter 2
	call non-virtual Nat._add
	goto endWhen
	else:
	get parameter 0
	call non-virtual Nat.decr
	get parameter 2
	get parameter 1
	get parameter 2
	call non-virtual Nat._add
	tail call non-virtual Recur.fibonacci-recursive
	endWhen:
	return

