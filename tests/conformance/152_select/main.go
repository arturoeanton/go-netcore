package main

func main() {
	ch := make(chan int, 1)
	select {
	case v := <-ch:
		println("got", v)
	default:
		println("empty")
	}
	ch <- 42
	select {
	case v := <-ch:
		println("got", v)
	default:
		println("empty")
	}
	full := make(chan int, 1)
	full <- 1
	select {
	case full <- 2:
		println("sent")
	default:
		println("full")
	}
	done := make(chan int)
	go func() { done <- 99 }()
	select {
	case v := <-done:
		println("done", v)
	}
}
