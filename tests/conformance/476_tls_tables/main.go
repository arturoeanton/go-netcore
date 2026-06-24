package main
import ("crypto/tls";"fmt")
func main(){
 fmt.Println(tls.CipherSuiteName(0x1301))
 fmt.Println(tls.CipherSuiteName(0xc02f))
 fmt.Println(tls.CipherSuiteName(0x9999)) // unknown
 fmt.Println(tls.VersionName(tls.VersionTLS13))
 fmt.Println(tls.VersionName(tls.VersionTLS12))
 fmt.Println(tls.VersionName(0x9999))
 for _,c:=range []tls.ClientAuthType{tls.NoClientCert,tls.RequestClientCert,tls.RequireAndVerifyClientCert,tls.ClientAuthType(9)}{
  fmt.Println(c.String())
 }
 for _,cv:=range []tls.CurveID{tls.CurveP256,tls.CurveP384,tls.X25519,tls.CurveID(99)}{
  fmt.Println(cv.String())
 }
}
