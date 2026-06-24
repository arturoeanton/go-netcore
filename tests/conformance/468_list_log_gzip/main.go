package main

import (
	"bytes"
	"compress/gzip"
	"container/list"
	"fmt"
	"io"
	"log"
)

func dump(l *list.List) {
	for e := l.Front(); e != nil; e = e.Next() {
		fmt.Printf("%v ", e.Value)
	}
	fmt.Println()
}

func main() {
	l := list.New()
	e1 := l.PushBack(1)
	l.PushBack(2)
	e3 := l.PushBack(3)
	l.PushBack(4)
	dump(l)
	l.MoveAfter(e1, e3)
	dump(l)
	l.MoveBefore(e3, e1)
	dump(l)

	a := list.New()
	a.PushBack("a")
	a.PushBack("b")
	b := list.New()
	b.PushBack("x")
	b.PushBack("y")
	a.PushBackList(b)
	dump(a)
	a.PushFrontList(b)
	dump(a)

	var buf bytes.Buffer
	log.SetFlags(0)
	log.SetOutput(&buf)
	log.SetPrefix("P:")
	log.Println("hello", 42)
	log.Print("world")
	log.Default().Println("viadefault")
	log.Output(2, "outline")
	fmt.Print(buf.String())
	fmt.Println("writer==buf:", log.Writer() == &buf)

	var gz bytes.Buffer
	w, err := gzip.NewWriterLevel(&gz, gzip.BestCompression)
	fmt.Println("gzerr:", err)
	w.Write([]byte("hello gzip world"))
	w.Close()
	r, _ := gzip.NewReader(&gz)
	r.Multistream(false)
	out, _ := io.ReadAll(r)
	fmt.Printf("roundtrip=%q\n", out)
	_, e := gzip.NewWriterLevel(&gz, 99)
	fmt.Println("badlevel:", e)
}
