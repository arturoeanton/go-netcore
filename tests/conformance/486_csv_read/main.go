package main
import ("encoding/csv";"fmt";"strings";"bytes";"io")
func main(){
 r:=csv.NewReader(strings.NewReader("a,b,c\n1,\"x,y\",3\n\"quoted\nnewline\",p,q\n"))
 for {
  rec,err:=r.Read()
  if err==io.EOF { break }
  if err!=nil { fmt.Println("err:",err); break }
  fmt.Printf("%q\n",rec)
 }
 // field count mismatch
 r2:=csv.NewReader(strings.NewReader("a,b\n1,2,3\n"))
 r2.Read(); _,e:=r2.Read(); fmt.Println("mismatch:",e)
 // Writer WriteAll
 var buf bytes.Buffer
 w:=csv.NewWriter(&buf)
 w.WriteAll([][]string{{"x","y"},{"1","com,ma"},{"q\"q","n\nn"}})
 fmt.Printf("written=%q err=%v\n",buf.String(),w.Error())
 fmt.Println(csv.ErrFieldCount,csv.ErrBareQuote,csv.ErrQuote,csv.ErrTrailingComma)
}
