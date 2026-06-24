package main
import ("fmt";"net")
func main(){
 ip:=net.IPv4(192,168,1,1)
 fmt.Println(ip.String())
 fmt.Println(ip.DefaultMask().String())
 m:=net.CIDRMask(24,32)
 fmt.Println(m.String(), ip.Mask(m).String())
 o,b:=m.Size(); fmt.Println("size",o,b)
 fmt.Println("v4mask",net.IPv4Mask(255,255,0,0).String())
 mt,_:=ip.MarshalText(); fmt.Printf("text=%s\n",mt)
 at,_:=ip.AppendText([]byte("IP:")); fmt.Printf("append=%s\n",at)
 fmt.Println("ifacelocal", net.ParseIP("ff01::1").IsInterfaceLocalMulticast())
 fmt.Println(net.CIDRMask(31,32).String(), net.CIDRMask(129,128)==nil)
 mac,_:=net.ParseMAC("01:23:45:67:89:ab"); fmt.Println("mac",mac.String())
 f:=net.FlagUp|net.FlagBroadcast|net.FlagMulticast
 fmt.Println("flags",f.String())
 fmt.Println("flags0",net.Flags(0).String())
 _,n,_:=net.ParseCIDR("10.0.0.0/8"); fmt.Println("net",n.Network(),n.String())
 fmt.Println("v6",net.ParseIP("2001:db8::1").String())
 fmt.Println("loopmask",net.ParseIP("127.0.0.1").DefaultMask().String())
}
