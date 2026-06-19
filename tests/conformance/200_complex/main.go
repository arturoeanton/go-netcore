package main

func scale(c complex128, f float64) complex128 {
	return complex(real(c)*f, imag(c)*f)
}

func main() {
	a := complex(3, 4)
	b := complex(1, 2)
	c := a + b
	println(int(real(c)), int(imag(c)))
	d := a - b
	println(int(real(d)), int(imag(d)))
	m := a * b
	println(int(real(m)), int(imag(m)))
	q := complex(8, 0) / complex(2, 0)
	println(int(real(q)), int(imag(q)))
	n := -a
	println(int(real(n)), int(imag(n)))
	println(a == complex(3, 4))
	println(a == b)
	var z complex128
	println(int(real(z)), int(imag(z)))
	e := 2 + 3i
	println(int(real(e)), int(imag(e)))
	s := scale(a, 2)
	println(int(real(s)), int(imag(s)))
}
