package main
import ("fmt";"reflect";"sort")
func main(){
 s:=[]int{5,3,1,4,2}
 swap:=reflect.Swapper(s)
 swap(0,4); fmt.Println(s)
 swap(1,3); fmt.Println(s)
 // use with sort.Sort via a custom interface-free manual bubble using swapper
 a:=[]string{"banana","apple","cherry"}
 sw:=reflect.Swapper(a)
 // selection sort
 for i:=0;i<len(a);i++{ for j:=i+1;j<len(a);j++{ if a[j]<a[i]{ sw(i,j) } } }
 fmt.Println(a)
 // sort.Slice uses reflect.Swapper internally
 nums:=[]int{9,2,7,1,5}
 sort.Slice(nums,func(i,j int)bool{return nums[i]<nums[j]})
 fmt.Println(nums)
 // len 1 and 0
 one:=[]int{42}; reflect.Swapper(one)(0,0); fmt.Println(one)
}
