package main
import ("encoding/base64";"fmt";"io";"strings")
func main(){
 data:=[]byte("Hello, base64 world!")
 for _,e:=range []*base64.Encoding{base64.StdEncoding,base64.URLEncoding,base64.RawStdEncoding,base64.RawURLEncoding}{
  s:=e.EncodeToString(data)
  d,_:=e.DecodeString(s)
  fmt.Printf("%s -> %s\n",s,d)
 }
 // custom alphabet
 ce:=base64.NewEncoding("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_")
 fmt.Println(ce.EncodeToString(data))
 // WithPadding
 wp:=base64.StdEncoding.WithPadding('*')
 fmt.Println(wp.EncodeToString([]byte("ab")))
 np:=base64.StdEncoding.WithPadding(base64.NoPadding)
 fmt.Println(np.EncodeToString([]byte("ab")))
 // Append
 dst:=[]byte("P:")
 dst=base64.StdEncoding.AppendEncode(dst,[]byte("hi"))
 fmt.Printf("%s\n",dst)
 dd,_:=base64.StdEncoding.AppendDecode([]byte("X:"),[]byte("aGk="))
 fmt.Printf("%s\n",dd)
 // bad input -> CorruptInputError
 _,err:=base64.StdEncoding.DecodeString("!!!!")
 fmt.Println("err:",err)
 // NewDecoder
 r:=base64.NewDecoder(base64.StdEncoding,strings.NewReader("aGVsbG8="))
 out,_:=io.ReadAll(r); fmt.Printf("decoded=%q\n",out)
}
