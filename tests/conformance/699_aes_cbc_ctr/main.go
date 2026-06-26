package main

import (
	"bytes"
	"crypto/aes"
	"crypto/cipher"
	"fmt"
)

// crypto/cipher AES-CBC (NewCBCEncrypter/NewCBCDecrypter) and AES-CTR (NewCTR), byte-exact
// vs Go including the IV chaining across CryptBlocks calls and the big-endian CTR counter.
// Previously only AES-GCM was implemented.
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
	pt := []byte("This is a secret message padded!") // 32 bytes (2 blocks)

	enc := cipher.NewCBCEncrypter(block, iv)
	ct := make([]byte, len(pt))
	enc.CryptBlocks(ct, pt)
	fmt.Printf("cbc-enc: %x\n", ct)
	fmt.Println("blocksize:", enc.BlockSize())

	dec := cipher.NewCBCDecrypter(block, iv)
	back := make([]byte, len(ct))
	dec.CryptBlocks(back, ct)
	fmt.Println("cbc roundtrip:", bytes.Equal(back, pt))

	// streaming CBC (block-at-a-time) matches one-shot
	enc2 := cipher.NewCBCEncrypter(block, iv)
	out2 := make([]byte, 32)
	enc2.CryptBlocks(out2[:16], pt[:16])
	enc2.CryptBlocks(out2[16:], pt[16:])
	fmt.Println("cbc streaming matches:", bytes.Equal(out2, ct))

	ctr := cipher.NewCTR(block, iv)
	ctOut := make([]byte, len(pt))
	ctr.XORKeyStream(ctOut, pt)
	fmt.Printf("ctr: %x\n", ctOut)
	ctr2 := cipher.NewCTR(block, iv)
	ptBack := make([]byte, len(ctOut))
	ctr2.XORKeyStream(ptBack, ctOut)
	fmt.Println("ctr roundtrip:", bytes.Equal(ptBack, pt))

	// streaming CTR (uneven split) matches one-shot
	ctr3 := cipher.NewCTR(block, iv)
	s := make([]byte, len(pt))
	ctr3.XORKeyStream(s[:5], pt[:5])
	ctr3.XORKeyStream(s[5:], pt[5:])
	fmt.Println("ctr streaming matches:", bytes.Equal(s, ctOut))

	// AES-256 CBC
	key32 := make([]byte, 32)
	for i := range key32 {
		key32[i] = byte(i)
	}
	b32, _ := aes.NewCipher(key32)
	o32 := make([]byte, 16)
	cipher.NewCBCEncrypter(b32, iv).CryptBlocks(o32, pt[:16])
	fmt.Printf("aes256-cbc: %x\n", o32)
}
