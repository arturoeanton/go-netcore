package main

func main() {
	for n := 0; n < 5; n++ {
		switch n {
		case 0:
			println("zero")
			fallthrough
		case 1:
			println("one")
			fallthrough
		case 2:
			println("two")
		case 3:
			println("three")
		default:
			println("other", n)
		}
		println("---", n)
	}

	// fallthrough into default.
	switch x := 7; x {
	case 7:
		println("seven")
		fallthrough
	default:
		println("default branch")
	}
}
