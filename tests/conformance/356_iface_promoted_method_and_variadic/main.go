package main

import "fmt"

type Exporter interface {
	Export() string
	Sum(xs ...int) int
}

type base struct{ tag string }

func (b base) Export() string    { return "base:" + b.tag }
func (b base) Sum(xs ...int) int { s := 0; for _, x := range xs { s += x }; return s }

type wrapped struct{ base } // promotes base.Export + base.Sum

type other struct{ n int }

func (o other) Export() string    { return fmt.Sprintf("other:%d", o.n) }
func (o other) Sum(xs ...int) int { return o.n }

func main() {
	vals := []Exporter{wrapped{base{"x"}}, other{5}}
	for _, v := range vals {
		fmt.Println(v.Export(), v.Sum(), v.Sum(1, 2, 3))
	}
}
