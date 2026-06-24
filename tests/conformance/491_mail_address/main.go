package main
import ("fmt";"net/mail")
func main(){
 a,_:=mail.ParseAddress("Barry Gibbs <bg@example.com>"); fmt.Printf("%q\n",a.String())
 b,_:=mail.ParseAddress("plain@example.com"); fmt.Printf("%q\n",b.String())
 c,_:=mail.ParseAddress(`"Gibbs, Barry" <bg@x.com>`); fmt.Printf("%q\n",c.String())
 list,_:=mail.ParseAddressList("a@x.com, Bob <b@y.com>")
 for _,e:=range list { fmt.Printf("  %q\n",e.String()) }
 d:=&mail.Address{Name:"Café Münch",Address:"u@x.com"}; fmt.Printf("utf8=%q\n",d.String())
 _,err:=mail.ParseAddressList("not valid @@"); fmt.Println("err nil?",err==nil)
 fmt.Println(mail.ErrHeaderNotPresent)
}
