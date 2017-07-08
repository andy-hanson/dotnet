Int s1() [PrivateScope, Public, Virtual, Abstract]

Int s1Def(Super1) [PrivateScope, Public, Static]

s1Def
	this
	call virtual Super1.s1
	const 10
	call non-virtual Int.of
	call non-virtual Int._mul
	return

