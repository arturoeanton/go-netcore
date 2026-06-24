package main
import ("fmt";"net/http")
func main(){
 for _,v:=range []string{"HTTP/1.1","HTTP/1.0","HTTP/2.0","HTTP/1.9","HTTP/x.1","BOGUS","HTTP/1.11"}{
  ma,mi,ok:=http.ParseHTTPVersion(v); fmt.Printf("%q -> %d %d %v\n",v,ma,mi,ok)
 }
 cs,err:=http.ParseCookie("foo=bar; baz=qux")
 fmt.Println("err",err,"n",len(cs))
 for _,c:=range cs { fmt.Printf("  %s=%s\n",c.Name,c.Value) }
 _,e1:=http.ParseCookie("foo=bar; =bad"); fmt.Println("invalidname:",e1)
 _,e2:=http.ParseCookie(""); fmt.Println("blank:",e2)
 _,ev:=http.ParseCookie("k=a;b"); fmt.Println("badval:",ev)
 sc,err3:=http.ParseSetCookie("session=abc123; Path=/; Domain=example.com; Max-Age=3600; Secure; HttpOnly")
 fmt.Println("err3",err3)
 fmt.Printf("  name=%s val=%s path=%s dom=%s maxage=%d sec=%v http=%v\n",sc.Name,sc.Value,sc.Path,sc.Domain,sc.MaxAge,sc.Secure,sc.HttpOnly)
 fmt.Println("setstr:",sc.String())
 _,e4:=http.ParseSetCookie("noequals"); fmt.Println("noeq:",e4)
}
