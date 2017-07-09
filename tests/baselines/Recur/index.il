Void main() [PrivateScope, Public, Static]

Int f(Int) [PrivateScope, Public, Static]

main
	const 10
	call non-virtual Int.of
	call non-virtual Recur.f
	declare local x
	set local x
	get local x
	const 0
	call non-virtual Int.of
	call non-virtual Int._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

f
	get parameter 0
	const 0
	call non-virtual Int.of
	call non-virtual Int._eq
	get instance field Bool.value
	goto if false: else
	const 0
	call non-virtual Int.of
	goto endWhen
	else:
	get parameter 0
	const 1
	call non-virtual Int.of
	call non-virtual Int._sub
	declare local m
	set local m
	get local m
	tail call non-virtual Recur.f
	endWhen:
	return

