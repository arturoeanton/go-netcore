package main
import ("fmt";"strconv";"hash/crc32")
func main(){
 var pb,gb []byte
 var pc,gc int
 for r:=rune(0); r<0x12000; r++{
  if strconv.IsPrint(r){pc++;pb=append(pb,1)}else{pb=append(pb,0)}
  if strconv.IsGraphic(r){gc++;gb=append(gb,1)}else{gb=append(gb,0)}
 }
 fmt.Println("print",pc,crc32.ChecksumIEEE(pb))
 fmt.Println("graphic",gc,crc32.ChecksumIEEE(gb))
 fmt.Println(strconv.IsPrint('A'),strconv.IsPrint('\t'),strconv.IsPrint(0xad),strconv.IsPrint('世'),strconv.IsPrint(0x1F600))
 fmt.Println(strconv.IsGraphic(' '),strconv.IsGraphic(0xa0),strconv.IsGraphic('\n'))
}
