package main
import ("encoding/binary";"fmt";"unicode/utf16")
func main(){
 buf:=binary.AppendUvarint(nil,300)
 buf=binary.AppendVarint(buf,-5)
 fmt.Println(buf)
 b2,_:=binary.Append(nil,binary.BigEndian,uint32(0x01020304))
 fmt.Println(b2)
 buf3:=make([]byte,4);n,_:=binary.Encode(buf3,binary.LittleEndian,uint32(0x01020304));fmt.Println(n,buf3)
 var v uint32;binary.Decode([]byte{4,3,2,1},binary.LittleEndian,&v);fmt.Printf("%x\n",v)
 fmt.Println(binary.LittleEndian.Uint16([]byte{1,0}))
 fmt.Println(utf16.RuneLen('A'),utf16.RuneLen('𝄞'),utf16.RuneLen(-1))
 a:=utf16.AppendRune(nil,'A');a=utf16.AppendRune(a,'𝄞');fmt.Println(a)
}
