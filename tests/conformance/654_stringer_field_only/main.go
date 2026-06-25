package main
import "fmt"
// A Stringer type used ONLY as a struct field (never boxed into an interface
// elsewhere) must still have its String() invoked by fmt.
type Celsius float64
func (c Celsius) String() string { return fmt.Sprintf("%.1f°C", float64(c)) }
type Level int
func (l Level) String() string { return []string{"low","mid","high"}[l] }
type Reading struct{ Temp Celsius; Lvl Level; Raw int }
type Wrap struct{ R Reading; Name string }
func main(){
 r := Reading{Temp:21.5, Lvl:2, Raw:7}
 fmt.Println(r)
 fmt.Printf("%v\n", r)
 fmt.Printf("%+v\n", r)
 fmt.Println(&r)
 fmt.Println(Wrap{r, "x"})
 fmt.Printf("%+v\n", Wrap{r, "x"})
 fmt.Println([]Reading{r, {1.0, 0, 1}})
}
