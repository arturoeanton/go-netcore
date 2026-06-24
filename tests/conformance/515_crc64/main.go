package main
import ("fmt";"hash/crc64")
func main(){
 iso:=crc64.MakeTable(crc64.ISO)
 ecma:=crc64.MakeTable(crc64.ECMA)
 data:=[]byte("The quick brown fox jumps over the lazy dog")
 fmt.Printf("iso  %016x\n",crc64.Checksum(data,iso))
 fmt.Printf("ecma %016x\n",crc64.Checksum(data,ecma))
 fmt.Printf("empty-iso %016x\n",crc64.Checksum([]byte{},iso))
 // running hash via New
 h:=crc64.New(ecma)
 h.Write([]byte("The quick brown fox ")); h.Write([]byte("jumps over the lazy dog"))
 fmt.Printf("new-ecma %016x size=%d\n",h.Sum64(),h.Size())
 h.Reset(); h.Write(data); fmt.Printf("reset %016x\n",h.Sum64())
 // Update incremental
 c:=crc64.Update(0,iso,[]byte("hello")); c=crc64.Update(c,iso,[]byte(" world"))
 fmt.Printf("update %016x\n",c)
 fmt.Printf("once %016x\n",crc64.Checksum([]byte("hello world"),iso))
}
