package main
import ("fmt";"math")
func main(){
 // Erfinv/Erfcinv byte-exact on the polynomial path (|x|<=0.85).
 for _,x:=range []float64{0,0.1,0.25,0.5,0.7,0.85,-0.1,-0.5,-0.85,0.333,-0.666}{
  fmt.Printf("%v ",math.Erfinv(x))
 }
 fmt.Println()
 for _,x:=range []float64{1,0.75,0.5,0.25,1.5,1.75,1.25}{ // 1-x in [-0.75,0.85]
  fmt.Printf("%v ",math.Erfcinv(x))
 }
 fmt.Println()
 fmt.Println(math.Erfinv(1),math.Erfinv(-1),math.IsNaN(math.Erfinv(2)),math.IsNaN(math.Erfinv(-2)),math.Erfcinv(0),math.Erfcinv(2))
}
