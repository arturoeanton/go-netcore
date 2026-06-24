package main
import ("fmt";"net/netip")
func main(){
 aps:=[]string{"1.2.3.4:80","[2001:db8::1]:8080","[fe80::1%eth0]:22"}
 for _,s:=range aps{
  ap:=netip.MustParseAddrPort(s)
  mt,_:=ap.MarshalText(); mb,_:=ap.MarshalBinary()
  fmt.Printf("%q text=%s bin=%v\n",s,mt,mb)
  var u netip.AddrPort; u.UnmarshalText(mt)
  var ub netip.AddrPort; ub.UnmarshalBinary(mb)
  fmt.Printf("  rtT=%v rtB=%v eqT=%v eqB=%v\n",u,ub,u==ap,ub==ap)
 }
 pfs:=[]string{"10.0.0.0/8","2001:db8::/32","::1/128","192.168.1.0/24"}
 for _,s:=range pfs{
  p:=netip.MustParsePrefix(s)
  mt,_:=p.MarshalText(); mb,_:=p.MarshalBinary()
  fmt.Printf("%q text=%s bin=%v\n",s,mt,mb)
  var u netip.Prefix; u.UnmarshalText(mt)
  var ub netip.Prefix; ub.UnmarshalBinary(mb)
  fmt.Printf("  rtT=%v rtB=%v eqT=%v eqB=%v\n",u,ub,u==p,ub==p)
 }
 var z netip.AddrPort; mt,_:=z.MarshalText(); mb,_:=z.MarshalBinary(); fmt.Printf("zero text=%q bin=%v\n",mt,mb)
 var zp netip.Prefix; pt,_:=zp.MarshalText(); fmt.Printf("zeroP text=%q\n",pt)
}
