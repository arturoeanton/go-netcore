package main
import ("fmt";"path";"path/filepath";"encoding/hex";"encoding/base64";"math/big";"strings")
func main(){
	// path.Clean edge cases
	for _,p := range []string{"", ".", "..", "/", "//", "a//b", "a/./b", "a/../b", "../a", "/../a", "a/b/../../c", "./", "a/"} {
		fmt.Printf("Clean(%q)=%q Dir=%q Base=%q\n", p, path.Clean(p), path.Dir(p), path.Base(p))
	}
	fmt.Println(filepath.Join("a","","b"), filepath.Ext(""), filepath.Ext(".gitignore"))
	// hex edge cases
	fmt.Println(hex.EncodeToString([]byte{}), hex.EncodeToString([]byte{0,255,16}))
	_,e := hex.DecodeString("xyz"); fmt.Println(e != nil)
	_,e2 := hex.DecodeString("abc"); fmt.Println(e2 != nil) // odd length
	// base64 edge cases
	fmt.Println(base64.StdEncoding.EncodeToString([]byte{}), base64.StdEncoding.EncodeToString([]byte{1}))
	fmt.Println(base64.RawStdEncoding.EncodeToString([]byte("foob")))
	// big edge cases
	a := big.NewInt(-100); b := big.NewInt(7)
	fmt.Println(new(big.Int).Div(a,b), new(big.Int).Mod(a,b), new(big.Int).Neg(a), a.Sign())
	z := new(big.Int); z.SetString("0", 10); fmt.Println(z, z.Sign())
	// strings.Cut with empty
	x,y,f := strings.Cut("abc", ""); fmt.Println(x,y,f)
	c1,c2,c3 := strings.Cut("nodelim", "="); fmt.Println(c1,c2,c3)
}
