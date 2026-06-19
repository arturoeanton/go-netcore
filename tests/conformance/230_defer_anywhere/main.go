package main

type Res struct{ name string }

func (r *Res) Close() { println("close", r.name) }

func worker(id int) (status string) {
	defer func() {
		if r := recover(); r != nil {
			status = "failed"
		}
	}()
	r := &Res{name: "r"}
	defer r.Close()
	for i := 0; i < 3; i++ {
		defer println("loop-defer", i)
		if i == 2 && id < 0 {
			panic("bad id")
		}
	}
	if id < 0 {
		panic("neg")
	}
	return "ok"
}

func main() {
	// defer in nested block
	{
		defer println("block-defer")
		println("in-block")
	}

	// conditional defer
	x := true
	if x {
		defer println("cond-defer")
	}

	println("worker(5):", worker(5))
	println("worker(-1):", worker(-1))

	// per-iteration recover via closure
	for i := 0; i < 3; i++ {
		func(n int) {
			defer func() {
				if r := recover(); r != nil {
					println("iter-recovered", n)
				}
			}()
			if n == 1 {
				panic("x")
			}
			println("iter-ok", n)
		}(i)
	}

	println("main-end")
}
