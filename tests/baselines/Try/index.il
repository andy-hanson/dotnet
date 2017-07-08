Void main() [PrivateScope, Public, Static]

main
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
	const "unreachable"
	call non-virtual String.of
	set local tryResult
	catch Builtins+Exception
	declare local e
	set local e
	get local e
	call virtual Exception.description
	set local tryResult
	end try
	get local tryResult
	declare local x
	set local x
	get local x
	const "Assertion failed."
	call non-virtual String.of
	call non-virtual String._eq
	get instance field Bool.value
	goto if true: endAssert
	new AssertionException
	throw
	endAssert:
	load static field Void.instance
	return

