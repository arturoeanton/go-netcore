// http-basic: a real HTTP server + client round-trip in one process — a REST API
// returning JSON, exercised by an in-process client. The shape of a SaaS endpoint.
package main

import (
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"sort"
)

type Item struct {
	ID    int     `json:"id"`
	Name  string  `json:"name"`
	Price float64 `json:"price"`
}

var catalog = []Item{
	{1, "Coffee", 3.50},
	{2, "Tea", 2.75},
	{3, "Cocoa", 4.00},
}

func main() {
	http.HandleFunc("/items", func(w http.ResponseWriter, r *http.Request) {
		b, _ := json.Marshal(catalog)
		w.Write(b)
	})
	http.HandleFunc("/health", func(w http.ResponseWriter, r *http.Request) {
		w.Write([]byte("ok"))
	})

	go http.ListenAndServe("127.0.0.1:18099", nil)

	ready := make(chan struct{})
	go func() {
		for {
			resp, err := http.Get("http://127.0.0.1:18099/health")
			if err == nil {
				resp.Body.Close()
				close(ready)
				return
			}
		}
	}()
	<-ready

	resp, err := http.Get("http://127.0.0.1:18099/items")
	if err != nil {
		fmt.Println("get error:", err)
		return
	}
	body, _ := io.ReadAll(resp.Body)
	resp.Body.Close()

	var items []Item
	json.Unmarshal(body, &items)
	sort.Slice(items, func(i, j int) bool { return items[i].Price < items[j].Price })
	fmt.Printf("status=%d items=%d\n", resp.StatusCode, len(items))
	for _, it := range items {
		fmt.Printf("  #%d %-8s $%.2f\n", it.ID, it.Name, it.Price)
	}
}
