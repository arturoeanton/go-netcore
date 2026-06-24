package main
import ("fmt";"os";"encoding/asn1";"errors")
func main(){
 se:=os.NewSyscallError("read",errors.New("boom"))
 fmt.Println(se); fmt.Printf("%v|%s\n",se,se)
 st:=asn1.StructuralError{Msg:"bad"}
 fmt.Println(st); fmt.Printf("%v\n",st)
 sy:=asn1.SyntaxError{Msg:"oops"}
 fmt.Println(sy)
 // error wrapping/Is
 var e error=se; fmt.Println(e.Error())
}
