package main
import ("fmt";"text/template")
type S struct{X int}
func main(){
 vals:=[]any{true,false,0,1,-5,uint(0),uint(7),0.0,3.14,"","hi",[]int{},[]int{1},map[string]int{},map[string]int{"a":1},nil,S{},&S{},complex(0,0),complex(1,0),int8(0),int8(2)}
 for i,v:=range vals{ t,ok:=template.IsTrue(v); fmt.Printf("%d truth=%v ok=%v\n",i,t,ok) }
 var np *S
 t,ok:=template.IsTrue(np); fmt.Printf("nilptr truth=%v ok=%v\n",t,ok)
}
