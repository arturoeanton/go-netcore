package main
import ("fmt";"reflect")
func main(){
	fmt.Println(reflect.DeepEqual([]int{1,2}, []int{1,2}))
	fmt.Println(reflect.DeepEqual([]int{1,2}, []int{1,3}))
	fmt.Println(reflect.DeepEqual(map[string]int{"a":1}, map[string]int{"a":1}))
	fmt.Println(reflect.DeepEqual(map[string]int{"a":1}, map[string]int{"a":2}))
	var zv reflect.Value
	fmt.Println(zv.IsValid())
	fmt.Println(reflect.ValueOf(42).Kind().String())
	fmt.Println(reflect.ValueOf("x").Kind().String())
	fmt.Println(reflect.ValueOf(3.14).Kind().String())
	m := map[string]int{"a":1,"b":2}
	mv := reflect.ValueOf(m)
	sum := 0
	for _, k := range mv.MapKeys() {
		sum += int(mv.MapIndex(k).Int())
	}
	fmt.Println("sum", sum)
}
