package main
import ("fmt";"net/url")
func main(){
	u,err := url.Parse("https://user@example.com:8080/path/to?q=1&x=2#frag")
	fmt.Println(err)
	fmt.Println(u.Scheme, u.Host, u.Path, u.RawQuery, u.Fragment)
	u2,_ := url.Parse("/relative/path?a=b")
	fmt.Println(u2.Scheme, u2.Path, u2.RawQuery)
	u3,_ := url.Parse("mailto:foo@bar.com")
	fmt.Println(u3.Scheme, u3.Opaque)
}
