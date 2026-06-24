package main
import ("fmt";"net/url")
func main(){
 u,_:=url.Parse("https://user:pass@host.com:8443/a/b?x=1&y=2#frag")
 fmt.Println(u.Hostname(),u.Port())
 fmt.Println(u.EscapedPath(),u.EscapedFragment())
 fmt.Println(u.User.Username())
 pw,ok:=u.User.Password();fmt.Println(pw,ok)
 fmt.Println(u.Redacted())
 fmt.Println(u.String())
 // url.User / UserPassword
 ui:=url.UserPassword("bob","secret");fmt.Println(ui.String())
 ui2:=url.User("alice");fmt.Println(ui2.String())
 // ParseQuery
 q,_:=url.ParseQuery("a=1&a=2&b=hello%20world")
 fmt.Println(q.Get("a"),q["a"],q.Get("b"))
 // JoinPath
 j,_:=url.JoinPath("https://x.com/api","v1","../v2","users")
 fmt.Println(j)
 ju:=u.JoinPath("c","d");fmt.Println(ju.Path)
 // Parse relative
 ref,_:=u.Parse("/other?z=9");fmt.Println(ref.String())
 // MarshalBinary
 b,_:=u.MarshalBinary();fmt.Println(string(b))
}
