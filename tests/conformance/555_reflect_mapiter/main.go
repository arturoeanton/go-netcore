package main

import (
	"fmt"
	"reflect"
	"sort"
)

type Greeter struct{}

func (Greeter) Hello() string { return "hi" }
func (Greeter) Wave() int     { return 1 }
func (Greeter) secret() int   { return 0 } // unexported: excluded by Type.Method

func main() {
	// Method.IsExported (type with only exported methods so NumMethod matches Go).
	t := reflect.TypeOf(Greeter{})
	var ms []string
	for i := 0; i < t.NumMethod(); i++ {
		m := t.Method(i)
		ms = append(ms, fmt.Sprintf("%s=%v", m.Name, m.IsExported()))
	}
	sort.Strings(ms)
	fmt.Println("methods", ms)

	// MapRange iteration (sorted for determinism).
	m := map[string]int{"alpha": 1, "beta": 2, "gamma": 3}
	v := reflect.ValueOf(m)
	it := v.MapRange()
	var pairs []string
	for it.Next() {
		pairs = append(pairs, fmt.Sprintf("%s=%d", it.Key().String(), it.Value().Int()))
	}
	sort.Strings(pairs)
	fmt.Println("range", pairs)

	// SetIterKey / SetIterValue into addressable destinations.
	it2 := v.MapRange()
	var keys []string
	var sum int64
	for it2.Next() {
		var k string
		var val int
		reflect.ValueOf(&k).Elem().SetIterKey(it2)
		reflect.ValueOf(&val).Elem().SetIterValue(it2)
		keys = append(keys, k)
		sum += int64(val)
	}
	sort.Strings(keys)
	fmt.Println("setiter", keys, sum)

	// Reset to a new map.
	it.Reset(reflect.ValueOf(map[string]int{"solo": 99}))
	var got []string
	for it.Next() {
		got = append(got, fmt.Sprintf("%s=%d", it.Key().String(), it.Value().Int()))
	}
	fmt.Println("reset", got)
}
