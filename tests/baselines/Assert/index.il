Void main() [PrivateScope, Public, Static]

main
	load static field Bool.boolTrue
	get instance field Bool.value
	goto if true: endAssert
	new Assertion_Exception
	throw
	endAssert:
	load static field Void.instance
	return

