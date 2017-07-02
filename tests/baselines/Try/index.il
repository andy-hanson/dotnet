fun main
	declare local tryResult
	begin try
	load static field Bool.boolFalse
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	pop
	const ""
	call static String.of
	set local tryResult
	catch Builtins+Exception
	declare local e
	set local e
	get local e
	call instance Exception.description
	set local tryResult
	end try
	get local tryResult
	return

