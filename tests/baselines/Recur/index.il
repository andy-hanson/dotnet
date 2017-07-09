Void main() [PrivateScope, Public, Static]

Nat f(Nat) [PrivateScope, Public, Static]

main
	const uint 10
	call non-virtual Nat.of
	call non-virtual Recur.f
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

f
	get parameter 0
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat._eq
	get instance field Bool.value
	goto if false: else
	const uint 0
	call non-virtual Nat.of
	goto endWhen
	else:
	get parameter 0
	const uint 1
	call non-virtual Nat.of
	call non-virtual Nat._sub
	call non-virtual Int.toNat
	tail call non-virtual Recur.f
	endWhen:
	return

