package main
import ("fmt";"errors";"encoding/csv")
func main(){
 es:=[]*csv.ParseError{
  {StartLine:3,Line:3,Column:5,Err:csv.ErrQuote},
  {StartLine:2,Line:4,Column:8,Err:csv.ErrBareQuote},
  {Line:7,Err:csv.ErrFieldCount},
  {StartLine:1,Line:1,Column:2,Err:errors.New("custom")},
 }
 for _,e:=range es{
  fmt.Println(e.Error())
  fmt.Println("  unwrap:",e.Unwrap(),"fieldcount:",errors.Is(e,csv.ErrFieldCount))
  fmt.Printf("  start=%d line=%d col=%d\n",e.StartLine,e.Line,e.Column)
 }
 // as error via fmt
 var err error = &csv.ParseError{Line:9,Column:1,Err:csv.ErrQuote}
 fmt.Printf("err=%v\n",err)
}
