package main

func inc(p *int) {
	*p = *p + 1
}

func main() {
	x := 10
	p := &x
	*p = 20
	println(x)
	*p = *p + 5
	println(x)
	inc(p)
	println(x)
	inc(&x)
	println(x)
}
