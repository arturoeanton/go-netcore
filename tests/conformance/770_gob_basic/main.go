package main

import (
	"bytes"
	"encoding/gob"
	"encoding/hex"
	"fmt"
)

type Point struct{ X, Y int }

type Person struct {
	Name string
	Age  int
	Tags []string
	Home Point
}

type Wrap struct {
	B  bool
	F  float64
	U  uint
	S  string
	By []byte
	P  *Point
	M  map[string]int
}

type Inner struct{ V int }

type Outer struct {
	I  Inner
	Is []Inner
}

type Roster []string

func dump(label string, f func(*gob.Encoder)) {
	var b bytes.Buffer
	enc := gob.NewEncoder(&b)
	f(enc)
	fmt.Println(label, hex.EncodeToString(b.Bytes()))
}

func main() {
	// --- deterministic wire bytes (hex) vs go run -------------------------------
	dump("int3:", func(e *gob.Encoder) { _ = e.Encode(3) })
	dump("intneg:", func(e *gob.Encoder) { _ = e.Encode(-7) })
	dump("int300:", func(e *gob.Encoder) { _ = e.Encode(300) })
	dump("uint9:", func(e *gob.Encoder) { _ = e.Encode(uint(9)) })
	dump("bool:", func(e *gob.Encoder) { _ = e.Encode(true) })
	dump("float:", func(e *gob.Encoder) { _ = e.Encode(17.0) })
	dump("float2:", func(e *gob.Encoder) { _ = e.Encode(3.14159) })
	dump("str:", func(e *gob.Encoder) { _ = e.Encode("hola") })
	dump("bytes:", func(e *gob.Encoder) { _ = e.Encode([]byte{1, 2, 250}) })
	dump("point:", func(e *gob.Encoder) { _ = e.Encode(Point{22, 33}) })
	dump("pointptr:", func(e *gob.Encoder) { _ = e.Encode(&Point{22, 33}) })
	dump("pointzero:", func(e *gob.Encoder) { _ = e.Encode(Point{}) })
	dump("twopoints:", func(e *gob.Encoder) { _ = e.Encode(Point{1, 2}); _ = e.Encode(Point{3, 4}) })
	dump("intslice:", func(e *gob.Encoder) { _ = e.Encode([]int{5, 6, 7}) })
	dump("strslice:", func(e *gob.Encoder) { _ = e.Encode([]string{"a", "bc"}) })
	dump("emptyslice:", func(e *gob.Encoder) { _ = e.Encode([]int{}) })
	dump("map1:", func(e *gob.Encoder) { _ = e.Encode(map[string]int{"k": 9}) })
	dump("person:", func(e *gob.Encoder) {
		_ = e.Encode(Person{Name: "Ana", Age: 30, Tags: []string{"x", "y"}, Home: Point{1, 2}})
	})
	dump("wrap:", func(e *gob.Encoder) {
		_ = e.Encode(Wrap{B: true, F: 2.5, U: 700, S: "s", By: []byte{9}, P: &Point{5, 6}, M: map[string]int{"a": 1}})
	})
	dump("wrapzero:", func(e *gob.Encoder) { _ = e.Encode(Wrap{}) })
	dump("arr:", func(e *gob.Encoder) { _ = e.Encode([3]int{7, 8, 9}) })
	dump("slicestruct:", func(e *gob.Encoder) { _ = e.Encode([]Point{{1, 2}, {3, 4}}) })
	dump("nested:", func(e *gob.Encoder) { _ = e.Encode(Outer{I: Inner{5}, Is: []Inner{{6}}}) })
	dump("namedslice:", func(e *gob.Encoder) { _ = e.Encode(Roster{"p", "q"}) })
	dump("int8:", func(e *gob.Encoder) { _ = e.Encode(int8(-2)) })
	dump("uint64big:", func(e *gob.Encoder) { _ = e.Encode(uint64(1 << 40)) })
	dump("float32:", func(e *gob.Encoder) { _ = e.Encode(float32(1.5)) })
	dump("mapii:", func(e *gob.Encoder) { _ = e.Encode(map[int]bool{3: true}) })

	// --- round-trips (decode correctness, incl. shapes with nondeterministic
	// encode order like multi-entry maps) ---------------------------------------
	var b bytes.Buffer
	enc := gob.NewEncoder(&b)
	_ = enc.Encode(Person{Name: "Luz", Age: 41, Tags: []string{"go", "clr"}, Home: Point{9, 8}})
	var p Person
	err := gob.NewDecoder(&b).Decode(&p)
	fmt.Println("rt-person:", err, p.Name, p.Age, p.Tags, p.Home.X, p.Home.Y)

	b.Reset()
	_ = gob.NewEncoder(&b).Encode(map[string]int{"a": 1, "b": 2, "c": 3})
	var m map[string]int
	err = gob.NewDecoder(&b).Decode(&m)
	fmt.Println("rt-map:", err, m["a"], m["b"], m["c"], len(m))

	b.Reset()
	_ = gob.NewEncoder(&b).Encode(Wrap{B: true, F: -0.25, U: 12, S: "zz", By: []byte{7, 8}, P: &Point{1, 1}, M: map[string]int{"m": 5}})
	var w Wrap
	err = gob.NewDecoder(&b).Decode(&w)
	fmt.Println("rt-wrap:", err, w.B, w.F, w.U, w.S, w.By, w.P.X, w.P.Y, w.M["m"])

	b.Reset()
	_ = gob.NewEncoder(&b).Encode([]float64{1.5, -2.25})
	var fs []float64
	err = gob.NewDecoder(&b).Decode(&fs)
	fmt.Println("rt-floats:", err, fs)

	b.Reset()
	_ = gob.NewEncoder(&b).Encode([3]int{4, 5, 6})
	var ar [3]int
	err = gob.NewDecoder(&b).Decode(&ar)
	fmt.Println("rt-arr:", err, ar[0], ar[1], ar[2])

	b.Reset()
	_ = gob.NewEncoder(&b).Encode("línea ñ")
	var s2 string
	err = gob.NewDecoder(&b).Decode(&s2)
	fmt.Println("rt-str:", err, s2)

	b.Reset()
	_ = gob.NewEncoder(&b).Encode(Outer{I: Inner{7}, Is: []Inner{{8}, {9}}})
	var o Outer
	err = gob.NewDecoder(&b).Decode(&o)
	fmt.Println("rt-nested:", err, o.I.V, len(o.Is), o.Is[0].V, o.Is[1].V)

	// two values on one stream, decoded in order
	b.Reset()
	e2 := gob.NewEncoder(&b)
	_ = e2.Encode(Point{10, 20})
	_ = e2.Encode(Point{30, 40})
	d2 := gob.NewDecoder(&b)
	var q1, q2 Point
	_ = d2.Decode(&q1)
	_ = d2.Decode(&q2)
	fmt.Println("rt-two:", q1.X, q1.Y, q2.X, q2.Y)

	// field matching by name: extra remote fields ignored, missing stay zero
	b.Reset()
	_ = gob.NewEncoder(&b).Encode(Point{7, 8})
	type P2 struct{ Y, Z int }
	var p2 P2
	err = gob.NewDecoder(&b).Decode(&p2)
	fmt.Println("rt-fieldmatch:", err, p2.Y, p2.Z)

	// widening: int32 wire value into an int64 local
	b.Reset()
	_ = gob.NewEncoder(&b).Encode(int32(1000))
	var big int64
	err = gob.NewDecoder(&b).Decode(&big)
	fmt.Println("rt-widen:", err, big)

	// errors
	b.Reset()
	_ = gob.NewEncoder(&b).Encode("text")
	var n int
	err = gob.NewDecoder(&b).Decode(&n)
	fmt.Println("err-wrongtype:", err)

	var empty bytes.Buffer
	err = gob.NewDecoder(&empty).Decode(&n)
	fmt.Println("err-eof:", err)

	// zero struct field always sent, nil map field skipped, zero complex skipped
	dump("hz-zero:", func(e *gob.Encoder) { _ = e.Encode(Hz{}) })
	// empty non-nil map IS sent (count 0); complex value bytes
	dump("hz-vals:", func(e *gob.Encoder) { _ = e.Encode(Hz{M: map[string]int{}, C: 2 + 3i}) })
	dump("complex:", func(e *gob.Encoder) { _ = e.Encode(4 + 5i) })

	b.Reset()
	_ = gob.NewEncoder(&b).Encode(Hz{P: Point{3, 4}, M: map[string]int{"q": 2}, C: 1 - 2i})
	var hz Hz
	err = gob.NewDecoder(&b).Decode(&hz)
	fmt.Println("rt-hz:", err, hz.P.X, hz.P.Y, hz.M["q"], hz.C == 1-2i)

	// interleaved encode/decode over one buffer (incremental stream)
	b.Reset()
	e3 := gob.NewEncoder(&b)
	d3 := gob.NewDecoder(&b)
	_ = e3.Encode(Point{100, 200})
	var r1 Point
	_ = d3.Decode(&r1)
	_ = e3.Encode(Point{300, 400})
	var r2 Point
	_ = d3.Decode(&r2)
	fmt.Println("rt-interleaved:", r1.X, r1.Y, r2.X, r2.Y)

	// bytes round-trip incl. high bytes
	b.Reset()
	_ = gob.NewEncoder(&b).Encode([]byte{0, 127, 128, 255})
	var bs []byte
	err = gob.NewDecoder(&b).Decode(&bs)
	fmt.Println("rt-bytes:", err, bs)
}

type Hz struct {
	P Point
	M map[string]int
	C complex128
}
