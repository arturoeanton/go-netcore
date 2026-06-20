package main
import ("fmt";"hash/fnv";"hash/crc32";"hash/adler32")
func main(){
	h := fnv.New32a(); h.Write([]byte("hello")); fmt.Println(h.Sum32())
	h2 := fnv.New64a(); h2.Write([]byte("hello")); fmt.Println(h2.Sum64())
	h3 := fnv.New32(); h3.Write([]byte("hello")); fmt.Println(h3.Sum32())
	fmt.Println(crc32.ChecksumIEEE([]byte("hello world")))
	fmt.Println(adler32.Checksum([]byte("hello world")))
	hc := fnv.New64(); hc.Write([]byte("a")); hc.Write([]byte("b")); fmt.Println(hc.Sum64())
}
