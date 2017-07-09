Void main() [PrivateScope, Public, Static]

Int double(Int) [PrivateScope, Public, Static]

main
	const 1
	call non-virtual Int.of
	const 2
	call non-virtual Int.of
	call non-virtual Int._add
	const 3
	call non-virtual Int.of
	call non-virtual Int._mul
	declare local x
	set local x
	get local x
	const 9
	call non-virtual Int.of
	call non-virtual Int._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	pop
	get local x
	call non-virtual OperatorParsing.double
	const 1
	call non-virtual Int.of
	call non-virtual Int._add
	declare local y
	set local y
	get local y
	const 19
	call non-virtual Int.of
	call non-virtual Int._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	pop
	get local x
	const 1
	call non-virtual Int.of
	call non-virtual Int._add
	call non-virtual OperatorParsing.double
	declare local z
	set local z
	get local z
	const 20
	call non-virtual Int.of
	call non-virtual Int._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

double
	get parameter 0
	const 2
	call non-virtual Int.of
	call non-virtual Int._mul
	return

