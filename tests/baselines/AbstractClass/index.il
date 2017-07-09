String s() [PrivateScope, Public, Virtual, Abstract]

Void main(AbstractClass) [PrivateScope, Public, Static]

main
	get parameter 0
	call virtual AbstractClass.s
	const string "s"
	call non-virtual String.of
	call non-virtual String._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

