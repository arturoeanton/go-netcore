// regexp.(*Regexp).FindAllStringSubmatch — every match's submatches as [][]string.
package main

import (
	"fmt"
	"regexp"
)

func main() {
	re := regexp.MustCompile(`(\w+)=(\d+)`)
	ms := re.FindAllStringSubmatch("a=1 bb=22 ccc=333", -1)
	for _, m := range ms {
		fmt.Printf("%s/%s/%s ", m[0], m[1], m[2])
	}
	fmt.Println(len(ms))
	fmt.Println(re.FindAllStringSubmatch("none", -1) == nil)
}
