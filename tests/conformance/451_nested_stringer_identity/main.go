package main

import "fmt"

// Stringer identity is recovered in nested positions: a struct field, a map key,
// and a map value; and a precisely-typed slice field names its element under %#v.
type Suit int

const (
	Hearts Suit = iota
	Diamonds
	Clubs
	Spades
)

func (s Suit) String() string { return []string{"Hearts", "Diamonds", "Clubs", "Spades"}[s] }

type Card struct {
	Suit  Suit
	Rank  int
	Items []int
}

func main() {
	// Stringer struct field
	deck := []Card{{Hearts, 1, []int{1, 2}}, {Spades, 13, nil}}
	fmt.Println(deck)
	fmt.Printf("%v\n", deck)
	fmt.Printf("%+v\n", deck)

	// %#v names the []int field's element type precisely
	fmt.Printf("%#v\n", Card{Clubs, 7, []int{3, 4}})

	// Stringer map key and value, sorted by underlying key
	bySuit := map[Suit]int{Spades: 5, Hearts: 2, Clubs: 9}
	fmt.Println(bySuit)

	suitName := map[int]Suit{0: Hearts, 1: Spades}
	fmt.Println(suitName)
}
