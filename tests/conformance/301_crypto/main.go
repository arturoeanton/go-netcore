package main
import ("crypto/sha256";"crypto/sha1";"crypto/md5";"encoding/hex";"fmt")
func main(){
	h := sha256.New(); h.Write([]byte("hello")); fmt.Println(hex.EncodeToString(h.Sum(nil)))
	h2 := sha1.New(); h2.Write([]byte("hello")); fmt.Println(hex.EncodeToString(h2.Sum(nil)))
	h3 := md5.New(); h3.Write([]byte("hello world")); fmt.Println(hex.EncodeToString(h3.Sum(nil)))
	h4 := sha256.New(); h4.Write([]byte("a")); h4.Write([]byte("b")); fmt.Println(hex.EncodeToString(h4.Sum(nil)), h4.Size())
}
