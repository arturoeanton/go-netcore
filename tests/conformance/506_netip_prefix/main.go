package main
import ("fmt";"net/netip")
func main(){
 // AddrPort
 aps:=[]string{"1.2.3.4:80","[::1]:443","[2001:db8::1]:8080","[fe80::1%eth0]:22","255.255.255.255:65535"}
 for _,s:=range aps{
  ap,err:=netip.ParseAddrPort(s)
  fmt.Printf("%q -> %v err=%v addr=%v port=%d valid=%v\n",s,ap,err,ap.Addr(),ap.Port(),ap.IsValid())
 }
 badap:=[]string{"1.2.3.4","1.2.3.4:","[::1]","1.2.3.4:99999","[::1:80","::1:80","1.2.3.4:ab"}
 for _,s:=range badap{ _,err:=netip.ParseAddrPort(s); fmt.Printf("%q -> %v\n",s,err) }
 a:=netip.MustParseAddr("10.0.0.1"); ap2:=netip.AddrPortFrom(a,8000); fmt.Println(ap2,ap2.Compare(ap2))
 // Prefix
 pfs:=[]string{"10.0.0.0/8","192.168.1.0/24","2001:db8::/32","::1/128","0.0.0.0/0","10.1.2.3/16","fe80::/10"}
 for _,s:=range pfs{
  p,err:=netip.ParsePrefix(s)
  fmt.Printf("%q -> %v err=%v bits=%d valid=%v single=%v masked=%v\n",s,p,err,p.Bits(),p.IsValid(),p.IsSingleIP(),p.Masked())
 }
 badpf:=[]string{"10.0.0.0","10.0.0.0/33","2001:db8::/129","10.0.0.0/-1","10.0.0.0/x","fe80::1%eth0/10","1.2.3.4/08"}
 for _,s:=range badpf{ _,err:=netip.ParsePrefix(s); fmt.Printf("%q -> %v\n",s,err) }
 // Contains / Overlaps
 net1:=netip.MustParsePrefix("192.168.0.0/16")
 fmt.Println(net1.Contains(netip.MustParseAddr("192.168.5.5")),net1.Contains(netip.MustParseAddr("10.0.0.1")),net1.Contains(netip.MustParseAddr("::1")))
 net2:=netip.MustParsePrefix("192.168.1.0/24")
 fmt.Println(net1.Overlaps(net2),net2.Overlaps(net1),net1.Overlaps(netip.MustParsePrefix("10.0.0.0/8")))
 p6:=netip.MustParsePrefix("2001:db8::/32")
 fmt.Println(p6.Contains(netip.MustParseAddr("2001:db8::dead")),p6.Contains(netip.MustParseAddr("2001:db9::1")))
 pp,_:=netip.MustParseAddr("10.0.0.5").Prefix(24); fmt.Println(netip.PrefixFrom(a,24),pp)
}
