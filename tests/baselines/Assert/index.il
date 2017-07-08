Void main() [PrivateScope, Public, Static]

main
	load static field Bool.boolTrue
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

