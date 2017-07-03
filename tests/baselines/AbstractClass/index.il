fun main
	get parameter 0
	call instance AbstractClass.s
	const "s"
	call static String.of
	call instance String._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

