package main
import ("bytes";"fmt";"strings";"unicode")
func main(){
 fmt.Printf("%q\n",bytes.Fields([]byte("  a  b c ")))
 fmt.Printf("%q\n",bytes.SplitN([]byte("a,b,c,d"),[]byte(","),2))
 fmt.Printf("%q\n",bytes.SplitAfter([]byte("a,b,"),[]byte(",")))
 b,a,ok:=bytes.Cut([]byte("key=val"),[]byte("="));fmt.Printf("%q %q %v\n",b,a,ok)
 fmt.Println(bytes.ContainsAny([]byte("hello"),"xyz"),bytes.ContainsRune([]byte("héllo"),'é'))
 fmt.Println(bytes.ContainsFunc([]byte("a1b"),unicode.IsDigit),bytes.IndexFunc([]byte("ab3"),unicode.IsDigit))
 fmt.Printf("%q\n",bytes.Map(func(r rune)rune{return r+1},[]byte("abc")))
 fmt.Printf("%q %q\n",bytes.TrimLeft([]byte("xxabc"),"x"),bytes.TrimRight([]byte("abcyy"),"y"))
 fmt.Printf("%q\n",bytes.ToValidUTF8([]byte("a\xffb"),[]byte("?")))
 fmt.Println(bytes.LastIndexAny([]byte("go gopher"),"go"),bytes.LastIndexFunc([]byte("a1b2"),unicode.IsDigit))
 // Buffer methods
 var buf bytes.Buffer;buf.WriteString("line1\nline2\n")
 l,_:=buf.ReadBytes('\n');fmt.Printf("%q\n",l)
 buf.UnreadByte();fmt.Println(buf.Len())
 var bf bytes.Buffer;n,_:=bf.ReadFrom(strings.NewReader("from-reader"));fmt.Println(n,bf.String())
 // Reader methods (bytes.Reader)
 r:=bytes.NewReader([]byte("hello"));p:=make([]byte,3);rn,_:=r.ReadAt(p,2);fmt.Println(rn,string(p[:rn]))
 r.Seek(0,0);r.Reset([]byte("new"));fmt.Println(r.Len())
}
