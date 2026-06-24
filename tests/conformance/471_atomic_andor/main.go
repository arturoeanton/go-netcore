package main

import (
	"fmt"
	"sync/atomic"
)

func main() {
	var i32 int32 = 0b1100
	fmt.Println(atomic.AndInt32(&i32, 0b1010), i32)
	fmt.Println(atomic.OrInt32(&i32, 0b0001), i32)

	var i64 int64 = 0xff00
	fmt.Println(atomic.AndInt64(&i64, 0x0f00), i64)
	fmt.Println(atomic.OrInt64(&i64, 0x00ff), i64)

	var u64 uint64 = 0xff
	fmt.Println(atomic.AndUint64(&u64, 0x0f), u64)
	fmt.Println(atomic.OrUint64(&u64, 0xf0), u64)

	var u32 uint32 = 5
	fmt.Println(atomic.SwapUint32(&u32, 9), u32)
	fmt.Println(atomic.AndUint32(&u32, 0x8), u32)

	var up uintptr = 100
	fmt.Println(atomic.AddUintptr(&up, 5), atomic.LoadUintptr(&up))
	fmt.Println(atomic.SwapUintptr(&up, 1), up)
	fmt.Println(atomic.CompareAndSwapUintptr(&up, 1, 2), up)
	fmt.Println(atomic.AndUintptr(&up, 0), atomic.OrUintptr(&up, 7), up)

	// struct methods (Int32/Uint32 were previously broken; now int/uint-typed)
	var ai32 atomic.Int32
	ai32.Store(0b1111)
	fmt.Println(ai32.And(0b1010), ai32.Load())
	fmt.Println(ai32.Or(0b0100), ai32.Load())
	fmt.Println(ai32.Add(3), ai32.Swap(50), ai32.CompareAndSwap(50, 7), ai32.Load())

	var au32 atomic.Uint32
	au32.Store(0xff)
	fmt.Println(au32.And(0x0f), au32.Load())
	fmt.Println(au32.Or(0xf0), au32.Load())

	var ai64 atomic.Int64
	ai64.Store(0b1111)
	fmt.Println(ai64.And(0b1010), ai64.Or(0b0100), ai64.Load())

	var ap atomic.Uintptr
	ap.Store(8)
	fmt.Println(ap.And(12), ap.Or(2), ap.Load())
}
