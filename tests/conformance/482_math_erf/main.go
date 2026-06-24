package main
import ("fmt";"math")
func main(){
 // Erf: byte-exact on the polynomial range (|x|<1.25) and the saturated tail.
 for _,x:=range []float64{0,0.1,0.25,0.5,0.75,0.84375,1,1.1,1.2,3,5,6,7,-0.5,-1,-1.2,-3,-6,1e-10,1e-300}{
  fmt.Printf("%v ",math.Erf(x))
 }
 fmt.Println()
 // Erfc: byte-exact on |x|<1.25.
 for _,x:=range []float64{0,0.1,0.25,0.5,0.75,1,1.2,-0.1,-0.5,-1,-1.2}{
  fmt.Printf("%v ",math.Erfc(x))
 }
 fmt.Println()
 fmt.Println(math.Erf(math.Inf(1)),math.Erf(math.Inf(-1)),math.IsNaN(math.Erf(math.NaN())),
  math.Erfc(math.Inf(1)),math.Erfc(math.Inf(-1)),math.Erfc(30),math.Erfc(-30))
}
