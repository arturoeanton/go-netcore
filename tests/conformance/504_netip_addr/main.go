package main
import ("fmt";"net/netip")
func main(){
 v4:=netip.AddrFrom4([4]byte{192,168,1,5})
 lo:=netip.AddrFrom16([16]byte{15:1})
 ll:=netip.AddrFrom16([16]byte{0:0xfe,1:0x80,15:1})
 m6:=netip.AddrFrom16([16]byte{0:0xff,1:0x02,15:1})
 v4in6:=netip.AddrFrom16([16]byte{10:0xff,11:0xff,12:127,13:0,14:0,15:1})
 doc:=netip.AddrFrom16([16]byte{0x20,0x01,0x0d,0xb8,4:0,8:0,12:0,15:1})
 ula:=netip.AddrFrom16([16]byte{0:0xfd,15:2})
 var zero netip.Addr
 for _,a:=range []netip.Addr{v4,lo,ll,m6,v4in6,doc,ula,zero,netip.IPv6Loopback(),netip.IPv4Unspecified(),netip.IPv6Unspecified(),netip.IPv6LinkLocalAllNodes(),netip.IPv6LinkLocalAllRouters()}{
  fmt.Printf("%v | valid=%v is4=%v is6=%v is4in6=%v bitlen=%d zone=%q\n",a,a.IsValid(),a.Is4(),a.Is6(),a.Is4In6(),a.BitLen(),a.Zone())
  fmt.Printf("  loop=%v mc=%v uns=%v llu=%v llm=%v ilm=%v gu=%v priv=%v\n",a.IsLoopback(),a.IsMulticast(),a.IsUnspecified(),a.IsLinkLocalUnicast(),a.IsLinkLocalMulticast(),a.IsInterfaceLocalMulticast(),a.IsGlobalUnicast(),a.IsPrivate())
  fmt.Printf("  as16=%v slice=%v unmap=%v next=%v prev=%v\n",a.As16(),a.AsSlice(),a.Unmap(),a.Next(),a.Prev())
 }
 fmt.Println("cmp",v4.Compare(lo),lo.Compare(v4),v4.Less(lo),lo.Compare(lo))
 fmt.Println("as4",v4.As4(),v4in6.Unmap().As4())
 s,ok:=netip.AddrFromSlice([]byte{1,2,3,4}); fmt.Println(s,ok)
 _,ok2:=netip.AddrFromSlice([]byte{1,2,3}); fmt.Println(ok2)
}
