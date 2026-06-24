package main
import ("fmt";"mime")
func main(){
 var d mime.WordDecoder
 words:=[]string{
  "=?UTF-8?q?H=C3=A9llo?=","=?UTF-8?b?SMOpbGxv?=","=?UTF-8?q?=C2=A1Hola,_se=C3=B1or!?=",
  "=?ISO-8859-1?q?Caf=E9?=","=?ISO-8859-1?b?Q2Fm6Q==?=","=?us-ascii?q?plain?=",
  "=?UTF-8?q?ca=C3=B1a?=","notaword","=?UTF-8?x?bad?=","=?UTF-8?q?a=ZZ?=",
 }
 for _,w:=range words{
  s,err:=d.Decode(w)
  fmt.Printf("%q -> %q err=%v\n",w,s,err)
 }
 // DecodeHeader: mixed literal + words, adjacent words (whitespace collapse)
 hdrs:=[]string{
  "Subject: =?UTF-8?q?H=C3=A9llo?= World",
  "=?UTF-8?q?caf=C3=A9?= =?UTF-8?q?_bar?=",
  "plain text no words",
  "=?UTF-8?b?5pel5pys6Kqe?= and =?ISO-8859-1?q?Caf=E9?=",
  "=?UTF-8?q?a?==?UTF-8?q?b?=",
 }
 for _,h:=range hdrs{
  s,err:=d.DecodeHeader(h)
  fmt.Printf("hdr %q -> %q err=%v\n",h,s,err)
 }
}
