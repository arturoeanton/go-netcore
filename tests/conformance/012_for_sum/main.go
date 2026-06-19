package main

func main() {
	sum := 0
	for i := 1; i <= 10; i++ {
		sum += i
	}
	println(sum)

	n := 0
	for n < 100 {
		n += 7
	}
	println(n)

	count := 0
	for {
		count++
		if count == 5 {
			break
		}
	}
	println(count)

	total := 0
	for i := 0; i < 10; i++ {
		if i%2 == 1 {
			continue
		}
		total += i
	}
	println(total)
}
