Void main(Console_App) [PrivateScope, Public, Static]

main
	get parameter 0
	call virtual Console_App.stdout
	const string "Hello world!"
	call non-virtual String.of
	call virtual Write_Stream.write_line
	return

