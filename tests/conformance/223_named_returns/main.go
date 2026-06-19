package main

type myErr struct{ m string }

func (e *myErr) Error() string { return e.m }

func risky(n int) (result int) {
	defer func() {
		if r := recover(); r != nil {
			result = -1
		}
	}()
	if n < 0 {
		panic("negative")
	}
	return n * 2
}

func doThing(fail bool) (err error) {
	defer func() {
		if r := recover(); r != nil {
			err = &myErr{m: "recovered"}
		}
	}()
	if fail {
		panic("oops")
	}
	return nil
}

func split(sum int) (x, y int) {
	x = sum * 4 / 9
	y = sum - x
	return
}

func main() {
	println(risky(5))
	println(risky(-1))
	if doThing(false) == nil {
		println("ok")
	}
	if e := doThing(true); e != nil {
		println(e.Error())
	}
	a, b := split(17)
	println(a, b)
}
