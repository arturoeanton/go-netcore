package main

import "strconv"

func main() {
	println(strconv.Itoa(42), strconv.Itoa(-7))
	println(strconv.FormatInt(255, 16), strconv.FormatInt(10, 2), strconv.FormatInt(-255, 16))
	println(strconv.FormatBool(true), strconv.FormatBool(false))
	n, err := strconv.Atoi("123")
	println(n, err == nil)
	_, err = strconv.Atoi("abc")
	if err != nil {
		println(err.Error())
	}
	i, _ := strconv.ParseInt("ff", 16, 64)
	println(i)
	b, _ := strconv.ParseBool("true")
	println(b)
	bf, _ := strconv.ParseBool("nope")
	println(bf)
	println(strconv.Quote("a\tb\"c"))
}
