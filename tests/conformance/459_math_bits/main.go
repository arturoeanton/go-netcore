package main
import ("fmt";"math/bits")
func main(){
 s,c:=bits.Add64(0xFFFFFFFFFFFFFFFF,1,0);fmt.Println(s,c)
 s2,c2:=bits.Add32(0xFFFFFFFF,2,0);fmt.Println(s2,c2)
 d,b:=bits.Sub64(5,10,0);fmt.Println(d,b)
 hi,lo:=bits.Mul64(0xFFFFFFFFFFFFFFFF,2);fmt.Println(hi,lo)
 hi2,lo2:=bits.Mul32(0xFFFFFFFF,3);fmt.Println(hi2,lo2)
 q,r:=bits.Div64(1,0,3);fmt.Println(q,r)
 q2,r2:=bits.Div32(1,0,7);fmt.Println(q2,r2)
 fmt.Println(bits.Rem64(1,0,3),bits.Rem32(5,0,3))
 fmt.Printf("%b %b\n",bits.Reverse8(1),bits.Reverse(1))
 fmt.Printf("%x\n",bits.ReverseBytes(0x0102030405060708))
 fmt.Println(bits.RotateLeft(1,4),bits.RotateLeft(0x8000000000000000,1))
 // divide by zero panics
 defer func(){if e:=recover();e!=nil{fmt.Println("recovered:",e)}}()
 bits.Div64(0,10,0)
}
