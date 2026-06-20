package main

import "fmt"

type Stats struct {
	Count int
	Total int
}
type Account struct {
	Stats   Stats
	Balance int
	Owner   string
}
type Bank struct {
	Main Account
	Name string
}

func main() {
	// writing a field of a value-struct reached through a pointer
	a := &Account{Owner: "amy"}
	a.Stats.Count = 5
	a.Stats.Count++
	a.Stats.Total += 100
	a.Balance = 50
	fmt.Println(a.Stats.Count, a.Stats.Total, a.Balance, a.Owner)

	// three-level nesting through a pointer
	b := &Bank{Name: "acme"}
	b.Main.Stats.Count = 1
	b.Main.Stats.Count++
	b.Main.Stats.Total = 10
	b.Main.Stats.Total *= 3
	b.Main.Balance = 999
	b.Main.Owner = "bob"
	fmt.Println(b.Main.Stats.Count, b.Main.Stats.Total, b.Main.Balance, b.Main.Owner, b.Name)
}
