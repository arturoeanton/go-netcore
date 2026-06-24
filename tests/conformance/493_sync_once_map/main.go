package main
import ("fmt";"sync")
func main(){
 n:=0
 once:=sync.OnceFunc(func(){ n++ })
 once(); once(); once(); fmt.Println("oncefunc n=",n)
 calls:=0
 ov:=sync.OnceValue(func()int{ calls++; return 42 })
 fmt.Println(ov(),ov(),"calls=",calls)
 ovs:=sync.OnceValues(func()(int,string){ return 7,"x" })
 a,b:=ovs(); fmt.Println(a,b)
 // sync.Map
 var m sync.Map
 m.Store("k","v1")
 prev,loaded:=m.Swap("k","v2"); fmt.Println("swap",prev,loaded)
 fmt.Println("cas",m.CompareAndSwap("k","v2","v3"))
 v,_:=m.Load("k"); fmt.Println("after cas",v)
 fmt.Println("cad",m.CompareAndDelete("k","v3"))
 _,ok:=m.Load("k"); fmt.Println("deleted",!ok)
 m.Store("a",1); m.Clear()
 _,ok2:=m.Load("a"); fmt.Println("cleared",!ok2)
 // WaitGroup.Go
 var wg sync.WaitGroup
 total:=0; var mu sync.Mutex
 for i:=0;i<5;i++{ i:=i; wg.Go(func(){ mu.Lock(); total+=i; mu.Unlock() }) }
 wg.Wait(); fmt.Println("wg total",total)
}
