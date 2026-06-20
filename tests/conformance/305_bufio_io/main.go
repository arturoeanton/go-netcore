package main
import ("fmt";"strings";"bufio";"io";"bytes")
func main(){
	sc := bufio.NewScanner(strings.NewReader("line1\nline2\nline3"))
	for sc.Scan() { fmt.Println("got:", sc.Text()) }
	r := strings.NewReader("hello world")
	all,_ := io.ReadAll(r); fmt.Println(string(all), len(all))
	br := bytes.NewReader([]byte("a\nb"))
	sc2 := bufio.NewScanner(br); n := 0
	for sc2.Scan() { n++ }
	fmt.Println("lines:", n)
	var buf bytes.Buffer
	io.Copy(&buf, strings.NewReader("copied"))
	fmt.Println(buf.String())
}
