module.exports = cls => {
	class Impl extends cls {
		s() { return "s"; }
	}
	return cls.main(new Impl());
};
