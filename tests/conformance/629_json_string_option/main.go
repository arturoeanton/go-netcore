package main

import (
	"encoding/json"
	"fmt"
)

// The encoding/json ,string tag option encodes/decodes a bool/number/string scalar
// field as a quoted JSON string ("port":"8080"). Also json:"-" skips a field but
// json:"-," keeps a field literally named "-".
type Config struct {
	Port  int     `json:"port,string"`
	Debug bool    `json:"debug,string"`
	Ratio float64 `json:"ratio,string"`
	Big   int64   `json:"big,string"`
	U     uint    `json:"u,string"`
	S     string  `json:"s,string"`
	Plain int     `json:"plain"`
	Opt   int     `json:"opt,string,omitempty"`
	Name  string  `json:"-"`
	Dash  string  `json:"-,"`
}

func main() {
	c := Config{Port: 8080, Debug: true, Ratio: 1.5, Big: 9007199254740993, U: 42,
		S: "hi", Plain: 100, Opt: 5, Name: "ignored", Dash: "kept"}
	b, _ := json.Marshal(c)
	fmt.Println(string(b))

	var c2 Config
	json.Unmarshal([]byte(`{"port":"9090","debug":"false","ratio":"2.5","big":"9007199254740993","u":"7","s":"\"x\"","plain":100,"opt":"3","-":"d"}`), &c2)
	fmt.Println(c2.Port, c2.Debug, c2.Ratio, c2.Big, c2.U, c2.S, c2.Plain, c2.Opt, c2.Dash)

	// omitempty + string: a zero Opt is omitted
	c3 := Config{Port: 1, S: "y"}
	b3, _ := json.Marshal(c3)
	fmt.Println(string(b3))

	// case-insensitive key matching still applies with ,string
	var c4 Config
	json.Unmarshal([]byte(`{"PORT":"5","Debug":"true","RATIO":"3.0"}`), &c4)
	fmt.Println(c4.Port, c4.Debug, c4.Ratio)

	// unknown fields ignored
	var c5 Config
	err := json.Unmarshal([]byte(`{"port":"5","unknown":{"a":1}}`), &c5)
	fmt.Println(c5.Port, err)

	// round-trip preserves values
	rt, _ := json.Marshal(c)
	var c6 Config
	json.Unmarshal(rt, &c6)
	fmt.Println(c6.Port == c.Port, c6.Big == c.Big, c6.S == c.S, c6.Debug == c.Debug)
}
