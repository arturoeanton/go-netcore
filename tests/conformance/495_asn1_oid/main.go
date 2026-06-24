package main
import ("fmt";"encoding/asn1")
func main(){
 oid:=asn1.ObjectIdentifier{1,2,840,113549}
 fmt.Printf("%q %v %v\n",oid.String(),oid.Equal(asn1.ObjectIdentifier{1,2,840,113549}),oid.Equal(asn1.ObjectIdentifier{1,2,3}))
 fmt.Printf("%x\n",asn1.NullBytes)
 se:=asn1.StructuralError{Msg:"bad"}; fmt.Printf("%q msg=%q\n",se.Error(),se.Msg)
 sy:=asn1.SyntaxError{Msg:"oops"}; fmt.Printf("%q\n",sy.Error())
}
