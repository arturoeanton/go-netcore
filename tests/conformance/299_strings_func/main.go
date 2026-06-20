package main
import ("fmt";"strings")
func main(){
	isDigit := func(r rune)bool{ return r>='0'&&r<='9' }
	fmt.Printf("[%s]\n", strings.TrimFunc("123abc456", isDigit))
	fmt.Println(strings.IndexFunc("abc123", isDigit))
	fmt.Println(strings.FieldsFunc("a,b;c d", func(r rune)bool{ return r==','||r==';'||r==' ' }))
}
