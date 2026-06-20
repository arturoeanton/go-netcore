package main
import ("fmt";"strings";"bytes";"encoding/csv";"compress/gzip";"io")
func main(){
	// csv read
	r := csv.NewReader(strings.NewReader("a,b,c\n1,\"x,y\",3\n"))
	rows,_ := r.ReadAll()
	fmt.Println(len(rows), rows[0], rows[1])
	// csv write
	var buf bytes.Buffer
	w := csv.NewWriter(&buf)
	w.Write([]string{"name","age"})
	w.Write([]string{"alice","30"})
	w.Flush()
	fmt.Print(buf.String())
	// gzip roundtrip
	var gz bytes.Buffer
	zw := gzip.NewWriter(&gz)
	zw.Write([]byte("hello gzip world"))
	zw.Close()
	zr,_ := gzip.NewReader(&gz)
	dec,_ := io.ReadAll(zr)
	fmt.Println(string(dec), "compressed:", gz.Len() > 0)
}
