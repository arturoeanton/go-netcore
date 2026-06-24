package main
import ("fmt";"math/big")
func main(){
 vals:=[]string{"0","1","255","18446744073709551615","18446744073709551616","123456789012345678901234567890","-987654321"}
 for _,s:=range vals{
  z,_:=new(big.Int).SetString(s,10)
  w:=z.Bits()
  fmt.Printf("%s bits=%v\n",s,w)
  back:=new(big.Int).SetBits(append([]big.Word{},w...))
  fmt.Printf("  setbits=%s\n",back.String())
 }
 z2:=new(big.Int).SetBits([]big.Word{0xFFFFFFFFFFFFFFFF,0x1})
 fmt.Println("explicit",z2.String())
 // enum stringers via explicit String()
 fmt.Println(big.Below.String(),big.Exact.String(),big.Above.String())
 fmt.Println(big.ToNearestEven.String(),big.ToNearestAway.String(),big.ToZero.String(),big.AwayFromZero.String(),big.ToNegativeInf.String(),big.ToPositiveInf.String())
}
