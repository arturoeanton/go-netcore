package main

import (
	"fmt"
	"strconv"
)

// strconv.ParseFloat with bitSize 32 rounds to float32 precision (the returned float64
// reflects that rounding; float32 overflow is ErrRange). strconv.ParseInt/ParseUint
// with base 0 allow an underscore right after the base prefix (0x_FF) — the prefix
// counts as the preceding digit — but reject leading/trailing/doubled underscores.
func main() {
	// float32 precision + overflow
	for _, s := range []string{"0.1", "0.2", "1.5", "3.14159265358979", "1e38", "1e39", "-1e40", "16777217"} {
		f, err := strconv.ParseFloat(s, 32)
		fmt.Printf("%q:%v e=%v ", s, f, err != nil)
	}
	fmt.Println()

	// float64 keeps full precision
	f64, _ := strconv.ParseFloat("0.1", 64)
	fmt.Println(f64)

	// underscore positions (base 0)
	for _, s := range []string{"0x_FF", "0o_17", "0b_11", "0xF_F", "0_777", "1_2_3", "0x1_2_3", "0xFF_", "0x__FF", "_0xFF"} {
		n, err := strconv.ParseInt(s, 0, 64)
		fmt.Printf("%q:%d e=%v ", s, n, err != nil)
	}
	fmt.Println()

	// underscores are NOT allowed for an explicit base
	n1, e1 := strconv.ParseInt("1_000", 10, 64)
	fmt.Println(n1, e1 != nil)

	// ParseUint with prefix + grouped underscores
	u, _ := strconv.ParseUint("0x_dead_beef", 0, 64)
	fmt.Println(u)

	// FormatFloat at 32-bit vs 64-bit
	fmt.Println(strconv.FormatFloat(0.1, 'g', -1, 32), strconv.FormatFloat(0.1, 'g', -1, 64))
}
