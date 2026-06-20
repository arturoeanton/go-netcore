package main
import ("fmt";"strings")
func main(){
	s := "hello world"
	fmt.Println(s[0:5], s[6:], s[:5], s[2:4])
	fmt.Println(len(s[6:]))
	// common idiom: trim prefix manually
	if strings.HasPrefix(s, "hello") { fmt.Println(s[5:]) }
	u := "héllo" // multi-byte
	fmt.Println(u[0:1], u[0:3], len(u))
	for i := 0; i < len(s); i += 3 { 
		end := i+3
		if end > len(s) { end = len(s) }
		fmt.Print(s[i:end], "|")
	}
	fmt.Println()
}
