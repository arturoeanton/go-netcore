package main

import (
	"fmt"
	"net/http"
)

func main() {
	h := http.Header{}
	h.Add("X-Multi", "a")
	h.Add("x-multi", "b")
	h.Add("X-MULTI", "c")
	h.Set("Single", "only")

	// Values returns every value for the canonical key; Get returns just the first.
	vals := h.Values("X-Multi")
	fmt.Printf("values=%v len=%d get=%q\n", vals, len(vals), h.Get("X-Multi"))
	fmt.Printf("single=%v\n", h.Values("Single"))

	miss := h.Values("Absent")
	fmt.Printf("missing=%v len=%d nil=%v\n", miss, len(miss), miss == nil)

	// Clone is independent: mutating the clone must not touch the original.
	c := h.Clone()
	c.Add("X-Multi", "d")
	fmt.Printf("after clone-mutate: orig=%v clone=%v\n", h.Values("X-Multi"), c.Values("X-Multi"))

	for _, v := range h.Values("X-Multi") {
		fmt.Println("iter:", v)
	}
}
