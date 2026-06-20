package main
import ("fmt";"regexp")
func main(){
	re := regexp.MustCompile(`\d+`)
	fmt.Println(re.MatchString("abc123"), re.FindString("abc123def456"))
	fmt.Println(re.FindAllString("a1b2c3", -1))
	re2 := regexp.MustCompile(`(\w+)@(\w+)`)
	m := re2.FindStringSubmatch("user@host")
	fmt.Println(m[0], m[1], m[2])
	fmt.Println(re.ReplaceAllString("a1b2", "X"))
	ok,_ := regexp.MatchString(`^h.*o$`, "hello"); fmt.Println(ok)
	re3 := regexp.MustCompile(`,`)
	fmt.Println(re3.Split("a,b,c", -1))
	fmt.Println(regexp.QuoteMeta("a.b*c"))
}
