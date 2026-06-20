package main
import ("fmt";"strings";"unicode";"crypto/hmac";"crypto/sha256";"encoding/hex")
func main(){
	fmt.Printf("[%s]\n", strings.TrimFunc("  hi  ", unicode.IsSpace))
	fmt.Println(strings.IndexFunc("abc123", unicode.IsDigit))
	mac := hmac.New(sha256.New, []byte("secret"))
	mac.Write([]byte("message"))
	fmt.Println(hex.EncodeToString(mac.Sum(nil)))
	fmt.Println(hmac.Equal([]byte("ab"), []byte("ab")), hmac.Equal([]byte("ab"), []byte("ac")))
}
