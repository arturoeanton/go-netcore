package main
import ("fmt";"math")
func main(){
 // Gamma is byte-exact on the polynomial path (|x| <= 33; no Exp/Pow).
 for _,x:=range []float64{0.5,1,1.5,2,2.5,3,3.5,4,4.5,5,6,7,10,15,20,33,0.1,0.01,0.001,-0.5,-1.5,-2.5,-3.5,-10.3,-20.7}{
  fmt.Printf("%v ",math.Gamma(x))
 }
 fmt.Println()
 fmt.Println(math.Gamma(0),math.Gamma(-1),math.Gamma(math.Inf(1)),math.Gamma(math.Inf(-1)),math.IsNaN(math.Gamma(math.NaN())),math.Gamma(-0.0))
}
