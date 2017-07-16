"use strict";
const _ = require("nzlib");
module.exports = class Recur {
	static main() {
		if (Recur.factorial(5) !== 120)
			throw new _.Assertion_Exception();
		
		if (Recur.fibonacci(0) !== 0)
			throw new _.Assertion_Exception();
		
		if (Recur.fibonacci(2) !== 1)
			throw new _.Assertion_Exception();
		
		if (Recur.fibonacci(6) !== 8)
			throw new _.Assertion_Exception();
		
	}
	static factorial(n) {
		return Recur.factorial_recursive(n, 1);
	}
	static factorial_recursive(n, acc) {
		if (n === 0)
			return acc;
		else
			return Recur.factorial_recursive(_.Nat.decr(n), acc * n);
		
	}
	static fibonacci(n) {
		if (n === 0)
			return 0;
		else if (n === 1)
			return 1;
		else
			return Recur.fibonacci_recursive(n, 0, 1);
		
	}
	static fibonacci_recursive(n, acc1, acc2) {
		if (n === 2)
			return acc1 + acc2;
		else
			return Recur.fibonacci_recursive(_.Nat.decr(n), acc2, acc1 + acc2);
		
	}
};
