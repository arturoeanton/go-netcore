package main

import (
	"encoding/xml"
	"fmt"
	"sort"
)

func main() {
	// Spot-check well-known entities by their rune code points (avoids %q's
	// printable-classification, which is orthogonal to the table's contents).
	for _, k := range []string{"amp", "lt", "gt", "nbsp", "copy", "euro", "rsaquo", "Alpha", "hearts"} {
		var cps []int
		for _, r := range xml.HTMLEntity[k] {
			cps = append(cps, int(r))
		}
		fmt.Printf("%s=%v\n", k, cps)
	}
	fmt.Println("count", len(xml.HTMLEntity))

	// Deterministic full digest over every entry in sorted key order.
	keys := make([]string, 0, len(xml.HTMLEntity))
	for k := range xml.HTMLEntity {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	var sum, klen int
	for _, k := range keys {
		klen += len(k)
		for _, r := range xml.HTMLEntity[k] {
			sum = sum*31 + int(r)
		}
	}
	fmt.Println("runehash", sum, "keylen", klen)
}
