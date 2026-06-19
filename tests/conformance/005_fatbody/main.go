package main

// 12 calls => method body IL exceeds 64 bytes, forcing a fat method header.
func main() {
	println("l1")
	println("l2")
	println("l3")
	println("l4")
	println("l5")
	println("l6")
	println("l7")
	println("l8")
	println("l9")
	println("l10")
	println("l11")
	println("l12")
}
