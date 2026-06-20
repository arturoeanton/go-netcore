package main

import "fmt"

type Base struct {
	ID    int
	Label string
}

func (b Base) Describe() string  { return fmt.Sprintf("#%d/%s", b.ID, b.Label) }
func (b *Base) Bump()            { b.ID++ }
func (b *Base) Retag(s string)   { b.Label = s }

type User struct {
	Base
	Name string
}

type Account struct {
	*Base
	Active bool
}

type Admin struct {
	User // two-level embedding: Admin -> User -> Base
	Level int
}

func main() {
	// value embed: promoted field read/write/compound + value & pointer methods
	var u User
	u.ID = 10
	u.Label = "u"
	u.Name = "amy"
	u.ID += 5
	fmt.Println(u.ID, u.Name, u.Describe())
	u.Bump()
	u.Retag("U")
	fmt.Println(u.Describe())

	// pointer embed: promoted read/write/compound + methods through *Base
	a := Account{Base: &Base{ID: 1, Label: "a"}, Active: true}
	fmt.Println(a.ID, a.Label, a.Active, a.Describe())
	a.ID = 100
	a.ID += 1
	a.Bump()
	a.Retag("A")
	fmt.Println(a.Describe())

	// two-level value embedding
	var ad Admin
	ad.ID = 7
	ad.Label = "root"
	ad.Name = "boss"
	ad.Level = 9
	ad.Bump()
	fmt.Println(ad.Describe(), ad.Name, ad.Level)
}
