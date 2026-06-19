package main

func worker(ch chan int, n int) { ch <- n * n }

func producer(ch chan int) {
	for i := 1; i <= 5; i++ {
		ch <- i
	}
	close(ch)
}

func main() {
	ch := make(chan int)
	go worker(ch, 9)
	println(<-ch)

	pc := make(chan int)
	go producer(pc)
	sum := 0
	for v := range pc {
		sum += v
	}
	println(sum)

	s := make(chan string, 1)
	go func() { s <- "closure" }()
	println(<-s)
}
