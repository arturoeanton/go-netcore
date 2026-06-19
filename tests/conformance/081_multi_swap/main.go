package main

func main() {
	a, b := 1, 2
	a, b = b, a
	println(a, b)
	x, y, z := 10, 20, 30
	x, y, z = z, x, y
	println(x, y, z)
	i, j := 5, 7
	i, j = j*2, i*3
	println(i, j)
}
