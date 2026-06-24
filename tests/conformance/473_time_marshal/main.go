package main
import ("fmt";"time")
func main(){
 t:=time.Date(2009,time.November,10,23,4,5,123456789,time.UTC)
 // text roundtrip
 mt,_:=t.MarshalText()
 var t2 time.Time; t2.UnmarshalText(mt)
 fmt.Println("text rt:",t2.Equal(t))
 // json roundtrip
 mj,_:=t.MarshalJSON()
 var t3 time.Time; t3.UnmarshalJSON(mj)
 fmt.Println("json rt:",t3.Equal(t))
 // binary roundtrip
 mb,_:=t.MarshalBinary()
 var t4 time.Time; err:=t4.UnmarshalBinary(mb)
 fmt.Println("bin rt:",t4.Equal(t),"err:",err)
 // gob roundtrip
 gb,_:=t.GobEncode()
 var t5 time.Time; t5.GobDecode(gb)
 fmt.Println("gob rt:",t5.Equal(t))
 // bad binary
 var t6 time.Time
 fmt.Println("bad:",t6.UnmarshalBinary([]byte{9,9,9}))
 fmt.Println("nodata:",t6.UnmarshalBinary([]byte{}))
 // ZoneBounds
 s,e:=t.ZoneBounds(); fmt.Println("zb:",s.IsZero(),e.IsZero())
}
