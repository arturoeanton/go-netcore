package main
import ("fmt";"math/big")
func main(){
 x:=big.NewInt(0b110100)
 fmt.Println("bit2",x.Bit(2),"bit3",x.Bit(3),"tzb",x.TrailingZeroBits())
 fmt.Println("andnot",new(big.Int).AndNot(big.NewInt(0b1110),big.NewInt(0b0110)).Text(2))
 fmt.Println("setbit",new(big.Int).SetBit(big.NewInt(0b1000),0,1).Text(2))
 fmt.Println("mulrange",new(big.Int).MulRange(1,5).String())
 fmt.Println("binom",new(big.Int).Binomial(10,3).String(),new(big.Int).Binomial(52,5).String())
 f,acc:=big.NewInt(1<<60).Float64(); fmt.Println("float64",f,acc==big.Exact)
 mt,_:=big.NewInt(255).MarshalText(); fmt.Printf("text=%s\n",mt)
 mj,_:=big.NewInt(-42).MarshalJSON(); fmt.Printf("json=%s\n",mj)
 fmt.Printf("append=%s\n",big.NewInt(255).Append([]byte("0x"),16))
 var u big.Int; u.UnmarshalText([]byte("999")); fmt.Println("unmarshal",u.String())
 var j big.Int; j.UnmarshalJSON([]byte("12345")); fmt.Println("unmarshalj",j.String())
 fmt.Println("modinv",new(big.Int).ModInverse(big.NewInt(3),big.NewInt(11)).String())
}
