package main
import ("fmt";"math/rand")
func main(){
 r:=rand.New(rand.NewSource(42))
 for i:=0;i<4;i++{ fmt.Print(r.Int63()," ") }; fmt.Println()
 fmt.Println(r.Uint64(),r.Uint32(),r.Int31())
 fmt.Println(r.Int31n(1000),r.Intn(1000),r.Int63n(1000000))
 fmt.Printf("%v %v\n",r.Float64(),r.Float32())
 buf:=make([]byte,8); n,_:=r.Read(buf); fmt.Println(n,buf)
 r.Seed(99); fmt.Println(r.Int63())
 // Shuffle determinism
 a:=[]int{0,1,2,3,4,5,6,7,8,9}
 r2:=rand.New(rand.NewSource(7))
 r2.Shuffle(len(a),func(i,j int){a[i],a[j]=a[j],a[i]})
 fmt.Println(a)
 fmt.Println(r2.Perm(8))
}
