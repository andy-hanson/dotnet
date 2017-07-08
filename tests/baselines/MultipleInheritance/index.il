constructor
	this
	get parameter 0
	set instance field x
	return

Void main() [PrivateScope, Public, Static]

Int s1() [PrivateScope, Public, Final, Virtual]

s1
	this
	get instance field MultipleInheritance.x
	return

Int s2() [PrivateScope, Public, Final, Virtual]

s2
	this
	get instance field MultipleInheritance.x
	const 1
	call non-virtual Int.of
	call non-virtual Int._add
	return

main
	const 1
	call non-virtual Int.of
	new MultipleInheritance
	declare local x
	set local x
	get local x
	call non-virtual Super1.s1Def
	const 10
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
	call non-virtual Super2.s2Def
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

