package main
import ("fmt";"strings";"text/scanner")
func main(){
 src:=`package main
// a line comment
func add(a, b int) int { /* block */ return a + b*2 }
var s = "he\tllo"
var r = ` + "`raw\nstring`" + `
var c = 'x'
var f = 3.14e-2
var h = 0xFF
var u = "wörld"`
 var sc scanner.Scanner
 sc.Init(strings.NewReader(src))
 sc.Filename="t.go"
 for tok:=sc.Scan(); tok!=scanner.EOF; tok=sc.Scan() {
  fmt.Printf("%s\t%s\t%q\n",sc.Position.String(),scanner.TokenString(tok),sc.TokenText())
 }
 fmt.Println("errors",sc.ErrorCount)
}
