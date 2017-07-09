Void main() [PrivateScope, Public, Static]

Void needsNat(Nat) [PrivateScope, Public, Static]

Void needsInt(Int) [PrivateScope, Public, Static]

Void needsReal(Real) [PrivateScope, Public, Static]

main
	const uint 0
	call non-virtual Nat.of
	call non-virtual Literals.needsNat
	pop
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat.toInt
	call non-virtual Literals.needsInt
	pop
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat.toReal
	call non-virtual Literals.needsReal
	pop
	const int 0
	call non-virtual Int.of
	call non-virtual Int.toNat
	call non-virtual Literals.needsNat
	pop
	const int 0
	call non-virtual Int.of
	call non-virtual Literals.needsInt
	pop
	const int 0
	call non-virtual Int.of
	call non-virtual Int.toReal
	call non-virtual Literals.needsReal
	pop
	const double 0
	call non-virtual Real.of
	call non-virtual Real.roundDown
	call non-virtual Int.toNat
	call non-virtual Literals.needsNat
	pop
	const double 0
	call non-virtual Real.of
	call non-virtual Real.roundUp
	call non-virtual Literals.needsInt
	pop
	const double 0
	call non-virtual Real.of
	call non-virtual Literals.needsReal
	return

needsNat
	load static field Void.instance
	return

needsInt
	load static field Void.instance
	return

needsReal
	load static field Void.instance
	return

