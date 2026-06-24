package main
import ("fmt";"net";"net/netip")
func main(){
 aps:=[]string{"1.2.3.4:80","127.0.0.1:8080","[::1]:443","[2001:db8::1]:9000","255.255.255.255:65535"}
 for _,s:=range aps{
  ap:=netip.MustParseAddrPort(s)
  t:=net.TCPAddrFromAddrPort(ap)
  u:=net.UDPAddrFromAddrPort(ap)
  fmt.Printf("%q tcp=%s net=%s port=%d | udp=%s net=%s\n",s,t.String(),t.Network(),t.Port,u.String(),u.Network())
  fmt.Printf("   tcpIP=%s udpIP=%s len=%d\n",t.IP.String(),u.IP.String(),len(t.IP))
 }
}
