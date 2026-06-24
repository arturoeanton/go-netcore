package main
import ("fmt";"strings";"mime/quotedprintable")
func readAll(s string)(string,error){
 r:=quotedprintable.NewReader(strings.NewReader(s))
 buf:=make([]byte,256)
 var out []byte
 for {
  n,err:=r.Read(buf)
  out=append(out,buf[:n]...)
  if err!=nil{
   if err.Error()=="EOF"{ return string(out),nil }
   return string(out),err
  }
 }
}
func main(){
 cases:=[]string{
  "Hello, World!","Caf=C3=A9 r=C3=A9sum=C3=A9","a=3Db",
  "long line with a soft=\r\nbreak in the middle","soft=\nbreak unix",
  "trailing=20space","literal = sign not hex","bad=ZZhex","=A","trailing ws  \r\nnext",
 }
 for _,s:=range cases{
  out,err:=readAll(s)
  fmt.Printf("%q -> %q err=%v\n",s,out,err)
 }
}
