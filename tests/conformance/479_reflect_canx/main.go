package main
import ("fmt";"reflect")
func main(){
 // CanInt/CanUint/CanFloat/CanComplex on values where goclr's Kind is precise.
 for _,x:=range []interface{}{int(1),uint(3),float64(2.5),complex128(3+4i),"s",true}{
  v:=reflect.ValueOf(x)
  fmt.Printf("%v: int=%v uint=%v float=%v complex=%v\n",v.Kind(),v.CanInt(),v.CanUint(),v.CanFloat(),v.CanComplex())
 }
 // ChanDir.String on the direction constants.
 fmt.Println(reflect.RecvDir.String(), reflect.SendDir.String(), reflect.BothDir.String())
}
