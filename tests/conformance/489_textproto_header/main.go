package main
import ("fmt";"net/textproto")
func main(){
 h:=make(textproto.MIMEHeader)
 h.Set("content-type","text/html")
 h.Add("X-Custom","a")
 h.Add("x-custom","b")
 fmt.Printf("%q\n",h.Get("Content-Type"))
 fmt.Printf("%q\n",h.Get("X-CUSTOM"))
 fmt.Printf("%q\n",h.Values("x-custom"))
 fmt.Printf("%q\n",h.Get("missing"))
 h.Del("content-type")
 fmt.Printf("%q after del\n",h.Get("Content-Type"))
 fmt.Println(textproto.CanonicalMIMEHeaderKey("x-foo-bar"))
 fmt.Printf("%q\n",textproto.TrimString("  hi \t "))
 var nilh textproto.MIMEHeader
 fmt.Printf("nil get=%q\n",nilh.Get("x"))
}
