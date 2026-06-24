package main
import ("fmt";"runtime/debug")
func main(){
 fmt.Println(debug.SetMaxStack(2000000000))
 fmt.Println(debug.SetMaxStack(3000000000)) // returns the value we just set
 fmt.Println(debug.SetMaxThreads(20000))
 fmt.Println(debug.SetMemoryLimit(-1))      // read-only, returns default
 fmt.Println(debug.SetMemoryLimit(500000000), debug.SetMemoryLimit(-1)) // set, then read
 fmt.Println(debug.SetPanicOnFault(true), debug.SetPanicOnFault(false))
 fmt.Println(debug.SetGCPercent(50), debug.SetGCPercent(75))
 debug.SetTraceback("all"); debug.FreeOSMemory()
 fmt.Println("ok")
}
