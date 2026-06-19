package main

func join(xs []string, sep string) string {
	out := ""
	for i, x := range xs {
		if i > 0 {
			out += sep
		}
		out += x
	}
	return out
}

func main() {
	println(join([]string{"a", "b", "c"}, "-"))
	s := "x"
	s += "y"
	s += "z"
	println(s)
	var u uint64 = 100
	u /= 7
	u %= 9
	println(u)
	c := complex(1, 1)
	c += complex(2, 3)
	c *= complex(2, 0)
	println(int(real(c)), int(imag(c)))
}
