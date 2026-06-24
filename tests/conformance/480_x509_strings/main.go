package main
import ("fmt";"crypto/x509")
func main(){
 for i:=0;i<6;i++{ fmt.Printf("%q ",x509.PublicKeyAlgorithm(i).String()) }
 fmt.Println()
 for i:=0;i<20;i++{ fmt.Printf("%q ",x509.SignatureAlgorithm(i).String()) }
 fmt.Println()
 for _,k:=range []x509.KeyUsage{1,2,4,8,16,32,64,128,256,5,0}{ fmt.Printf("%q ",k.String()) }
 fmt.Println()
 for i:=0;i<16;i++{ fmt.Printf("%q ",x509.ExtKeyUsage(i).String()) }
 fmt.Println()
}
