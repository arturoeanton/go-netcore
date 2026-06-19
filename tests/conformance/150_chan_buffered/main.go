package main

func main() {
	ch := make(chan int, 3)
	ch <- 10
	ch <- 20
	ch <- 30
	println(len(ch), cap(ch))
	println(<-ch)
	println(<-ch)
	println(<-ch)

	// comma-ok on a closed, drained channel yields the zero value
	d := make(chan int, 1)
	d <- 5
	close(d)
	v, ok := <-d
	println(v, ok)
	v2, ok2 := <-d
	println(v2, ok2)

	// string channel
	s := make(chan string, 2)
	s <- "hi"
	s <- "bye"
	println(<-s, <-s)
}
