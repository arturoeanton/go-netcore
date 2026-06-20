package main
import ("fmt";"strings";"bytes";"encoding/csv";"compress/gzip";"crypto/aes";"crypto/cipher";"io";"math/big")
func main(){
	// csv edge: CRLF, quoted newline, empty fields, single col
	r := csv.NewReader(strings.NewReader("a,,c\r\n\"line\nbreak\",x,y\r\nsolo\r\n"))
	rows,_ := r.ReadAll()
	fmt.Println(len(rows))
	for _,row := range rows { fmt.Printf("%d:%q ", len(row), row) }
	fmt.Println()
	// aes 32-byte key + additional data
	key := []byte("0123456789abcdef0123456789abcdef")
	bl,_ := aes.NewCipher(key)
	g,_ := cipher.NewGCM(bl)
	nonce := make([]byte, g.NonceSize())
	ct := g.Seal(nil, nonce, []byte("data"), []byte("aad"))
	pt,_ := g.Open(nil, nonce, ct, []byte("aad"))
	fmt.Println(string(pt))
	_,e := g.Open(nil, nonce, ct, []byte("wrong"))
	fmt.Println("aad mismatch:", e != nil)
	// gzip empty
	var b bytes.Buffer
	w := gzip.NewWriter(&b); w.Close()
	zr,_ := gzip.NewReader(&b)
	d,_ := io.ReadAll(zr)
	fmt.Println("empty gzip len:", len(d))
	// big GCD + large
	a := new(big.Int); a.SetString("123456789012345678901234567890", 10)
	bb := big.NewInt(12345)
	fmt.Println(new(big.Int).GCD(nil,nil,a,bb), new(big.Int).Quo(a,bb), new(big.Int).Rem(a,bb))
}
