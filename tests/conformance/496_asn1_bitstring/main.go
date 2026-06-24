package main
import ("fmt";"encoding/asn1")
func main(){
 bs:=asn1.BitString{Bytes:[]byte{0x80,0x40},BitLength:10}
 fmt.Println(bs.At(0),bs.At(1),bs.At(9),bs.At(100),bs.BitLength)
 fmt.Printf("%x\n",bs.RightAlign())
 bs2:=asn1.BitString{Bytes:[]byte{0xff,0xc0},BitLength:10}
 fmt.Println(bs2.At(0),bs2.At(8),bs2.At(9))
 fmt.Printf("%x len=%d\n",bs2.RightAlign(),len(bs2.Bytes))
}
