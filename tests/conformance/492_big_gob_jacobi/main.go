package main
import ("fmt";"math/big")
func main(){
 for _,n:=range []int64{0,1,255,256,-255,1000000}{
  g,_:=big.NewInt(n).GobEncode(); fmt.Printf("gob(%d)=%x\n",n,g)
 }
 at,_:=big.NewInt(-789).AppendText([]byte("V:")); fmt.Printf("append=%s\n",at)
 // gob roundtrip
 g,_:=big.NewInt(123456789).GobEncode()
 var z big.Int; z.GobDecode(g); fmt.Println("rt",z.String())
 fmt.Println("jacobi",big.Jacobi(big.NewInt(5),big.NewInt(21)),big.Jacobi(big.NewInt(2),big.NewInt(15)),big.Jacobi(big.NewInt(1001),big.NewInt(9907)))
}
