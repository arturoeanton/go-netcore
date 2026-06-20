package main
import ("fmt";"strings";"bytes";"unicode/utf8";"math/bits";"sort")
func main(){
	b,a,f := strings.Cut("key=val","="); fmt.Println(b,a,f)
	fmt.Println(strings.IndexRune("héllo",'l'), strings.ContainsRune("abc",'b'), strings.IndexAny("xyz","z"))
	fmt.Println(strings.ToTitle("hello"), strings.SplitAfter("a,b,c",","))
	fmt.Println(strings.Map(func(r rune)rune{return r+1}, "abc"))
	fmt.Println(bytes.LastIndex([]byte("abcabc"),[]byte("bc")))
	fmt.Println(utf8.Valid([]byte("héllo")), utf8.ValidRune('A'))
	r,sz := utf8.DecodeRuneInString("héllo"); fmt.Println(r,sz)
	fmt.Println(bits.OnesCount8(255), bits.LeadingZeros8(1), bits.Len16(256), bits.RotateLeft8(1,1), bits.TrailingZeros32(8))
	xs := []float64{3,1,2}; sort.Float64s(xs); fmt.Println(xs, sort.Float64sAreSorted(xs))
	ss := []string{"b","a","c"}; sort.Strings(ss); fmt.Println(sort.SearchStrings(ss,"b"))
	fmt.Println(sort.Search(10, func(i int)bool{return i>=5}))
	sl := []int{3,1,2}; sort.Slice(sl, func(i,j int)bool{return sl[i]<sl[j]}); fmt.Println(sl)
}
