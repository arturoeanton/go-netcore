package main
import ("fmt";"reflect")
func main(){
 a:=[]int{1,2,3}
 b:=[]int{4,5,6}
 r:=reflect.AppendSlice(reflect.ValueOf(a),reflect.ValueOf(b))
 fmt.Println(r.Interface())
 // append to empty
 e:=reflect.AppendSlice(reflect.ValueOf([]string{}),reflect.ValueOf([]string{"x","y"}))
 fmt.Println(e.Interface())
 // strings
 s:=reflect.AppendSlice(reflect.ValueOf([]string{"a"}),reflect.ValueOf([]string{"b","c"}))
 fmt.Println(s.Interface(),s.Len())
 // MakeMapWithSize
 mt:=reflect.MakeMapWithSize(reflect.TypeOf(map[string]int{}),10)
 mt.SetMapIndex(reflect.ValueOf("k"),reflect.ValueOf(42))
 fmt.Println(mt.Interface(),mt.Len())
}
