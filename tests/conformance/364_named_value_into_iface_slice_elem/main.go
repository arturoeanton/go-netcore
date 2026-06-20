package main

import "fmt"

type instr interface{ exec() string }
type jmp int32

func (j jmp) exec() string { return fmt.Sprintf("jmp:%d", int32(j)) }

type loadV struct{ v int }

func (l loadV) exec() string { return fmt.Sprintf("load:%d", l.v) }

func main() {
	code := make([]instr, 2)
	code[0] = loadV{5}
	code[1] = jmp(0)
	code[1] = jmp(7) // patch, like a jump-target backpatch
	for _, in := range code {
		fmt.Println(in.exec())
	}
}
