package main
import ("fmt";"net/url";"encoding/binary")
func main(){
	fmt.Println(url.QueryEscape("a b&c=d"))
	fmt.Println(url.PathEscape("a/b c"))
	u,_ := url.QueryUnescape("a+b%26c%3Dd"); fmt.Println(u)
	b := make([]byte, 8)
	binary.LittleEndian.PutUint32(b, 0x01020304)
	fmt.Println(b[0], b[1], b[2], b[3])
	fmt.Println(binary.LittleEndian.Uint32(b))
	binary.BigEndian.PutUint16(b, 0x0102)
	fmt.Println(b[0], b[1], binary.BigEndian.Uint16(b))
	binary.LittleEndian.PutUint64(b, 0x0102030405060708)
	fmt.Println(binary.LittleEndian.Uint64(b))
}
