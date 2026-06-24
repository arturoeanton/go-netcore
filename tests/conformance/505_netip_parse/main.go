package main
import ("fmt";"net/netip")
func main(){
 good:=[]string{"192.168.1.5","127.0.0.1","0.0.0.0","255.255.255.255","::1","::","2001:db8::1","fe80::1","ff02::1","::ffff:127.0.0.1","2001:db8:0:0:1:0:0:1","fe80::1%eth0","1:2:3:4:5:6:7:8","::ffff:192.168.0.1","2001:db8::1:0:0:1"}
 for _,s:=range good{
  a,err:=netip.ParseAddr(s)
  fmt.Printf("%q -> %v err=%v exp=%v\n",s,a,err,a.StringExpanded())
  mt,_:=a.MarshalText(); mb,_:=a.MarshalBinary()
  fmt.Printf("   text=%s bin=%v\n",mt,mb)
  var u netip.Addr; u.UnmarshalText(mt)
  var ub netip.Addr; ub.UnmarshalBinary(mb)
  fmt.Printf("   rtT=%v rtB=%v eqT=%v eqB=%v\n",u,ub,u==a,ub==a)
 }
 bad:=[]string{"","x","1.2.3","1.2.3.4.5","256.0.0.1","01.2.3.4","1.2.3.","::1::2","12345::","gg::","1:2:3:4:5:6:7:8:9","fe80::%","2001:db8:::1"}
 for _,s:=range bad{ _,err:=netip.ParseAddr(s); fmt.Printf("%q -> %v\n",s,err) }
 fmt.Println(netip.MustParseAddr("10.0.0.1"))
 w:=netip.IPv6Loopback().WithZone("lo0"); fmt.Println(w,w.Zone())
}
