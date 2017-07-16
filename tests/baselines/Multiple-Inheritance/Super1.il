Nat s1() [PrivateScope, Public, Virtual, Abstract]

Nat s1Def(Super1) [PrivateScope, Public, Static]

s1Def
	this
	call virtual Super1.s1
	const uint 10
	call non-virtual Nat.of
	call non-virtual Nat._mul
	return

