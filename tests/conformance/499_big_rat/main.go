package main
import ("fmt";"math/big")
func main(){
 fmt.Println(big.NewRat(2,4).String(), big.NewRat(2,4).RatString())
 fmt.Println(big.NewRat(5,1).String(), big.NewRat(5,1).RatString())
 fmt.Println(big.NewRat(-6,8).String())
 a:=big.NewRat(1,3); b:=big.NewRat(1,6)
 fmt.Println(new(big.Rat).Add(a,b), new(big.Rat).Sub(a,b), new(big.Rat).Mul(a,b), new(big.Rat).Quo(a,b))
 fmt.Println(new(big.Rat).Neg(a), new(big.Rat).Inv(a))
 fmt.Println(a.Num(), a.Denom(), a.Sign(), a.IsInt(), big.NewRat(4,2).IsInt())
 fmt.Println(a.Cmp(b), a.Cmp(a))
 var r big.Rat; r.SetFrac64(7,2); fmt.Println(r.String())
 var r2 big.Rat; r2.SetString("3/9"); fmt.Println(r2.String())
 var r3 big.Rat; r3.SetInt64(8); fmt.Println(r3.String(), r3.RatString())
}
