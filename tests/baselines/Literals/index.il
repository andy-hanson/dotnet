Void main() [PrivateScope, Public, Static]

Void needs-nat(Nat) [PrivateScope, Public, Static]

Void needs-int(Int) [PrivateScope, Public, Static]

Void needs-real(Real) [PrivateScope, Public, Static]

main
	const uint 0
	call non-virtual Nat.of
	call non-virtual Literals.needs-nat
	pop
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat.to_int
	call non-virtual Literals.needs-int
	pop
	const uint 0
	call non-virtual Nat.of
	call non-virtual Nat.to_real
	call non-virtual Literals.needs-real
	pop
	const int 0
	call non-virtual Int.of
	call non-virtual Int.to_nat
	call non-virtual Literals.needs-nat
	pop
	const int 0
	call non-virtual Int.of
	call non-virtual Literals.needs-int
	pop
	const int 0
	call non-virtual Int.of
	call non-virtual Int.to_real
	call non-virtual Literals.needs-real
	pop
	const double 0
	call non-virtual Real.of
	call non-virtual Real.round_down
	call non-virtual Int.to_nat
	call non-virtual Literals.needs-nat
	pop
	const double 0
	call non-virtual Real.of
	call non-virtual Real.round_up
	call non-virtual Literals.needs-int
	pop
	const double 0
	call non-virtual Real.of
	call non-virtual Literals.needs-real
	return

needs-nat
	load static field Void.instance
	return

needs-int
	load static field Void.instance
	return

needs-real
	load static field Void.instance
	return

