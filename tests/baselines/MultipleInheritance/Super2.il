Nat s2() [PrivateScope, Public, Virtual, Abstract]

Nat s2Def(Super2) [PrivateScope, Public, Static]

s2Def
	this
	call virtual Super2.s2
	const uint 10
	call non-virtual Nat.of
	call non-virtual Nat._mul
	return

