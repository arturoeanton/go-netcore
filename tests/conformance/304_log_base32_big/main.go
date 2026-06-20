package main
import ("fmt";"log";"encoding/base32";"math/big")
func main(){
	log.SetFlags(0); log.SetPrefix("APP: ")
	log.Println("hello", 42)
	log.Printf("x=%d", 7)
	e := base32.StdEncoding.EncodeToString([]byte("hello"))
	fmt.Println(e)
	d,_ := base32.StdEncoding.DecodeString(e); fmt.Println(string(d))
	a := big.NewInt(12345678901234)
	b := big.NewInt(98765432109876)
	c := new(big.Int).Mul(a, b)
	fmt.Println(c.String())
	fmt.Println(new(big.Int).Add(a, b).String(), a.Cmp(b))
	f := new(big.Int); f.SetString("123456789012345678901234567890", 10)
	fmt.Println(f.String())
}
