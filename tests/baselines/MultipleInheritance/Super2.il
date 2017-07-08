Int s2() [PrivateScope, Public, Virtual, Abstract]

Int s2Def(Super2) [PrivateScope, Public, Static]

s2Def
	this
	call virtual Super2.s2
	const 10
	call non-virtual Int.of
	call non-virtual Int._mul
	return

