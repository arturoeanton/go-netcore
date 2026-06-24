package main
import ("fmt";"math";"math/cmplx")
func main(){
 z:=complex(3,4)
 fmt.Println(cmplx.Abs(z),cmplx.Phase(z))
 fmt.Println(cmplx.Conj(z),cmplx.Sqrt(z))
 fmt.Println(cmplx.Log(complex(1,1)),cmplx.Log10(complex(10,0)))
 r,th:=cmplx.Polar(complex(-3,-4)); fmt.Println(r,th)
 fmt.Println(cmplx.IsInf(cmplx.Inf()),cmplx.IsNaN(cmplx.NaN()))
 fmt.Println(cmplx.IsInf(z),cmplx.IsNaN(z))
 // formatter: imaginary part always carries a sign (incl. -0, +Inf, NaN)
 fmt.Println(complex(1,0),complex(1,math.Copysign(0,-1)),cmplx.Inf(),cmplx.NaN())
}
