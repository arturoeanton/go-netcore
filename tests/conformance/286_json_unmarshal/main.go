package main

import (
	"encoding/json"
	"fmt"
)

type Address struct {
	City string `json:"city"`
	Zip  string `json:"zip"`
}

type Person struct {
	Name   string   `json:"name"`
	Age    int      `json:"age"`
	Email  string   `json:"email,omitempty"`
	Active bool     `json:"active"`
	Score  float64  `json:"score"`
	Tags   []string `json:"tags"`
	Nums   []int    `json:"nums"`
	Addr   Address  `json:"addr"`
	Addrs  []Address `json:"addrs"`
}

func main() {
	data := []byte(`{"name":"Alice","age":30,"active":true,"score":9.5,"tags":["a","b"],"nums":[1,2,3],"addr":{"city":"NYC","zip":"10001"},"addrs":[{"city":"LA","zip":"90001"},{"city":"SF","zip":"94101"}]}`)
	var p Person
	if err := json.Unmarshal(data, &p); err != nil {
		fmt.Println("err:", err)
		return
	}
	fmt.Println(p.Name, p.Age, p.Active, p.Score)
	fmt.Println(p.Tags, p.Nums)
	fmt.Println(p.Addr.City, p.Addr.Zip)
	fmt.Println(len(p.Addrs), p.Addrs[0].City, p.Addrs[1].City)
	fmt.Printf("%+v\n", p)

	// generic map (access by key, order-independent)
	var m map[string]interface{}
	json.Unmarshal([]byte(`{"x":1,"y":"hi","z":true}`), &m)
	fmt.Println(m["x"], m["y"], m["z"], len(m))

	// top-level slice
	var xs []int
	json.Unmarshal([]byte(`[10,20,30]`), &xs)
	fmt.Println(xs, len(xs))

	// roundtrip
	out, _ := json.Marshal(p)
	fmt.Println(string(out))

	// error case
	var bad Person
	err := json.Unmarshal([]byte(`{not json}`), &bad)
	fmt.Println(err != nil)
}
