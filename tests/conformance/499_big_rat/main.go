package main
import ("fmt";"math/big")
func main(){
 fmt.Printf("%q %q %q %q %q\n",big.NewRat(1,4).FloatString(3),big.NewRat(2,3).FloatString(5),big.NewRat(-1,8).FloatString(2),big.NewRat(7,1).FloatString(0),big.NewRat(1,3).FloatString(0))
 f,e:=big.NewRat(1,4).Float64(); fmt.Println(f,e)
 f2,e2:=big.NewRat(1,3).Float64(); fmt.Println(f2,e2)
 g,_:=big.NewRat(3,7).GobEncode(); fmt.Printf("gob=%x\n",g)
 var rt big.Rat; rt.GobDecode(g); fmt.Println("gobrt",rt.String())
 mt,_:=big.NewRat(5,2).MarshalText(); fmt.Printf("text=%s\n",mt)
 mt2,_:=big.NewRat(8,2).MarshalText(); fmt.Printf("textint=%s\n",mt2)
 var r big.Rat; r.SetFloat64(0.25); fmt.Println("setf",r.String())
 var r2 big.Rat; r2.SetUint64(99); fmt.Println("setu",r2.String())
 ap,_:=big.NewRat(3,4).AppendText([]byte("R:")); fmt.Printf("app=%s\n",ap)
 var u big.Rat; u.UnmarshalText([]byte("10/4")); fmt.Println("unm",u.String())
}
