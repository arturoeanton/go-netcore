package main
import ("fmt";"crypto/subtle";"mime";"container/list";"os/exec")
func main(){
	fmt.Println(subtle.ConstantTimeCompare([]byte("ab"),[]byte("ab")), subtle.ConstantTimeCompare([]byte("ab"),[]byte("ac")))
	fmt.Println(mime.TypeByExtension(".json"), mime.TypeByExtension(".png"))
	l := list.New()
	l.PushBack(1); l.PushBack(2); l.PushFront(0)
	fmt.Println("len:", l.Len())
	for e := l.Front(); e != nil; e = e.Next() { fmt.Print(e.Value, " ") }
	fmt.Println()
	out,_ := exec.Command("echo", "hello", "world").Output()
	fmt.Print(string(out))
}
