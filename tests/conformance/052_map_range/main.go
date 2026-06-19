package main

func main() {
	m := map[int]int{1: 10, 2: 20, 3: 30, 4: 40}
	println(len(m))
	sum := 0
	keySum := 0
	for k, v := range m {
		sum += v
		keySum += k
	}
	println(keySum, sum)
	count := 0
	for range m {
		count++
	}
	println(count)
}
