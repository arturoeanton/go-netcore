package main

import (
	"bytes"
	"crypto/aes"
	"crypto/cipher"
	"fmt"
)

// crypto/cipher AES-CFB (128-bit full-block feedback), AES-OFB, and the raw cipher.Block
// Encrypt/Decrypt — byte-exact vs Go. CFB/CTR/OFB all satisfy cipher.Stream; the shim
// dispatches XORKeyStream on the concrete handle.
func main() {
	key := make([]byte, 16)
	for i := range key {
		key[i] = byte(i * 7)
	}
	iv := make([]byte, 16)
	for i := range iv {
		iv[i] = byte(i * 3)
	}
	block, _ := aes.NewCipher(key)
	msg := []byte("CFB and OFB stream test message!")

	cfbE := cipher.NewCFBEncrypter(block, iv)
	cfbOut := make([]byte, len(msg))
	cfbE.XORKeyStream(cfbOut, msg)
	fmt.Printf("cfb: %x\n", cfbOut)
	cfbBack := make([]byte, len(cfbOut))
	cipher.NewCFBDecrypter(block, iv).XORKeyStream(cfbBack, cfbOut)
	fmt.Println("cfb roundtrip:", bytes.Equal(cfbBack, msg))

	// CFB streaming (uneven split) matches one-shot
	cfbS := cipher.NewCFBEncrypter(block, iv)
	s := make([]byte, len(msg))
	cfbS.XORKeyStream(s[:7], msg[:7])
	cfbS.XORKeyStream(s[7:], msg[7:])
	fmt.Println("cfb streaming matches:", bytes.Equal(s, cfbOut))

	ofb := cipher.NewOFB(block, iv)
	ofbOut := make([]byte, len(msg))
	ofb.XORKeyStream(ofbOut, msg)
	fmt.Printf("ofb: %x\n", ofbOut)
	ofbBack := make([]byte, len(ofbOut))
	cipher.NewOFB(block, iv).XORKeyStream(ofbBack, ofbOut)
	fmt.Println("ofb roundtrip:", bytes.Equal(ofbBack, msg))

	dst := make([]byte, 16)
	block.Encrypt(dst, msg[:16])
	fmt.Printf("block-enc: %x\n", dst)
	back := make([]byte, 16)
	block.Decrypt(back, dst)
	fmt.Println("block roundtrip:", bytes.Equal(back, msg[:16]), block.BlockSize())
}
