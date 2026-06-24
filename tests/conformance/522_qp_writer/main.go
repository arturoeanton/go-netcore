package main
import ("fmt";"bytes";"mime/quotedprintable")
func main(){
 cases:=[]string{
  "Hello, World!",
  "Caf\xc3\xa9 r\xc3\xa9sum\xc3\xa9",                       // UTF-8 accents
  "a=b & c",
  "trailing space \nand tab\t\nend",
  "this is a really long line that should exceed seventy-six characters and force a soft line break somewhere in the middle of it for sure yes",
  "binary\x00\x01\x02\xff data",
  "line1\r\nline2\r\nline3",
 }
 for _,s:=range cases{
  var buf bytes.Buffer
  w:=quotedprintable.NewWriter(&buf)
  w.Write([]byte(s))
  w.Close()
  fmt.Printf("%q -> %q\n",s,buf.String())
 }
}
