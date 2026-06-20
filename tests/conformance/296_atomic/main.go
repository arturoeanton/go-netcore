package main
import ("fmt";"sync";"sync/atomic")
func main(){
	var c int64
	var wg sync.WaitGroup
	for i:=0;i<100;i++{ wg.Add(1); go func(){ defer wg.Done(); atomic.AddInt64(&c,1) }() }
	wg.Wait()
	fmt.Println(atomic.LoadInt64(&c))
	atomic.StoreInt64(&c, 50)
	fmt.Println(atomic.SwapInt64(&c, 7), atomic.LoadInt64(&c))
	fmt.Println(atomic.CompareAndSwapInt64(&c, 7, 99), atomic.LoadInt64(&c))
	var m sync.Map
	a, l := m.LoadOrStore("k", 1); fmt.Println(a, l)
	a2, l2 := m.LoadOrStore("k", 2); fmt.Println(a2, l2)
}
