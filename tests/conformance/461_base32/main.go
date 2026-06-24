package main
import ("encoding/base32";"fmt";"io";"strings")
func main(){
 e:=base32.StdEncoding.EncodeToString([]byte("hello"));fmt.Println(e)
 d,_:=base32.StdEncoding.DecodeString(e);fmt.Println(string(d))
 fmt.Println(base32.HexEncoding.EncodeToString([]byte("hello")))
 np:=base32.StdEncoding.WithPadding(base32.NoPadding)
 fmt.Println(np.EncodeToString([]byte("hi")))
 fmt.Println(base32.StdEncoding.EncodedLen(5),base32.StdEncoding.DecodedLen(8))
 fmt.Printf("%s\n",base32.StdEncoding.AppendEncode(nil,[]byte("ab")))
 buf:=make([]byte,base32.StdEncoding.EncodedLen(2));base32.StdEncoding.Encode(buf,[]byte("ab"));fmt.Println(string(buf))
 custom:=base32.NewEncoding("0123456789ABCDEFGHIJKLMNOPQRSTUV")
 fmt.Println(custom.EncodeToString([]byte("x")))
 dec:=base32.NewDecoder(base32.StdEncoding,strings.NewReader(e))
 all,_:=io.ReadAll(dec);fmt.Println(string(all))
}
