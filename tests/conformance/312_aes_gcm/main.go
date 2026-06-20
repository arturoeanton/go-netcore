package main
import ("fmt";"crypto/aes";"crypto/cipher")
func main(){
	key := []byte("0123456789abcdef") // 16 bytes
	block,_ := aes.NewCipher(key)
	gcm,_ := cipher.NewGCM(block)
	nonce := make([]byte, gcm.NonceSize())
	for i := range nonce { nonce[i] = byte(i) }
	plaintext := []byte("secret message")
	ct := gcm.Seal(nil, nonce, plaintext, nil)
	fmt.Println("encrypted len:", len(ct))
	pt,err := gcm.Open(nil, nonce, ct, nil)
	fmt.Println(string(pt), err)
	// tampered
	ct[0] ^= 1
	_,err2 := gcm.Open(nil, nonce, ct, nil)
	fmt.Println("tamper detected:", err2 != nil)
}
