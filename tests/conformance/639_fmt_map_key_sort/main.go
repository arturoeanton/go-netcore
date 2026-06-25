package main

import "fmt"

// fmt orders map keys by their type's natural order — integer/float keys NUMERICALLY
// (so 2 < 10, and negatives before positives), bool false<true, strings lexically —
// not by the lexical form of the key.
func main() {
	fmt.Printf("%v\n", map[int]string{3: "c", 1: "a", 2: "b", 10: "j"})
	fmt.Printf("%v\n", map[int]int{-3: 1, -1: 2, -2: 3, 0: 4, 100: 5})
	fmt.Printf("%v\n", map[int64]int{1000000: 1, 5: 2, 100: 3, 20: 4})
	fmt.Printf("%v\n", map[uint64]string{18446744073709551615: "max", 1: "a", 100: "b"})
	fmt.Printf("%v\n", map[uint8]string{255: "max", 0: "min", 128: "mid"})
	fmt.Printf("%v\n", map[rune]string{'z': "Z", 'a': "A", 'm': "M"})
	fmt.Printf("%v\n", map[float64]string{2.5: "b", 1.1: "a", 30.0: "c", 3.7: "d"})
	fmt.Printf("%v\n", map[bool]int{true: 1, false: 0})
	fmt.Printf("%v\n", map[string]int{"banana": 2, "apple": 1, "cherry": 3})

	// %#v and %+v with integer keys
	fmt.Printf("%#v\n", map[int]string{10: "j", 2: "b", 1: "a"})
	type S struct{ M map[int]int }
	fmt.Printf("%+v\n", S{M: map[int]int{30: 1, 5: 2, 100: 3}})

	// nested integer-key maps
	fmt.Printf("%v\n", map[int]map[int]int{2: {20: 1, 3: 2}, 1: {100: 1, 5: 2}})

	// big negative/positive spread
	fmt.Printf("%v\n", map[int]bool{-100: true, 50: false, -1: true, 0: false, 99: true})
}
