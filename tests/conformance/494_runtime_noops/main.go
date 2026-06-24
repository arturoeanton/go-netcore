package main
import ("fmt";"runtime")
func main(){
 fmt.Println("cgo",runtime.NumCgoCall())
 fmt.Println("mutexfrac",runtime.SetMutexProfileFraction(1),runtime.SetMutexProfileFraction(2))
 runtime.SetBlockProfileRate(1)
 runtime.SetCPUProfileRate(0)
 x:=42; runtime.KeepAlive(x)
 runtime.LockOSThread(); runtime.UnlockOSThread()
 type T struct{ v int }
 t:=&T{1}; runtime.SetFinalizer(t,func(*T){})
 fmt.Println("ok")
}
