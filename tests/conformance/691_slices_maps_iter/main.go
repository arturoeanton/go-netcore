// Regression: slices/maps/iter — Concat/Compact/Insert/Delete/DeleteFunc/BinarySearch/
// Reverse/IndexFunc/MaxFunc, maps Clone/Keys/Values/Equal/DeleteFunc, iter.Seq/Seq2 range-
// over-func with early break, slices.Collect — all byte-exact vs go run.
package main
import ("fmt";"slices";"maps";"iter")
func Count(n int) iter.Seq[int] {
 return func(yield func(int) bool) {
  for i := 0; i < n; i++ { if !yield(i) { return } }
 }
}
func Enumerate[T any](s []T) iter.Seq2[int, T] {
 return func(yield func(int, T) bool) {
  for i, v := range s { if !yield(i, v) { return } }
 }
}
func main(){
 // slices functions
 s := []int{3, 1, 4, 1, 5, 9, 2, 6}
 fmt.Println(slices.Concat([]int{1,2}, []int{3,4}, []int{5}))
 c := slices.Clone(s); slices.Sort(c)
 fmt.Println(c)
 fmt.Println(slices.Compact([]int{1,1,2,2,3,3,1}))
 fmt.Println(slices.Insert([]int{1,2,5}, 2, 3, 4))
 fmt.Println(slices.Delete([]int{1,2,3,4,5}, 1, 3))
 fmt.Println(slices.DeleteFunc([]int{1,2,3,4,5,6}, func(x int) bool { return x%2 == 0 }))
 sorted := []int{1,2,3,5,8,13}
 i, found := slices.BinarySearch(sorted, 5)
 fmt.Println(i, found)
 fmt.Println(slices.Equal([]int{1,2}, []int{1,2}))
 r := []int{1,2,3}; slices.Reverse(r); fmt.Println(r)
 fmt.Println(slices.IndexFunc(s, func(x int) bool { return x > 4 }))
 fmt.Println(slices.ContainsFunc(s, func(x int) bool { return x == 9 }))
 fmt.Println(slices.MaxFunc(s, func(a, b int) int { return a - b }))
 // maps functions
 m := map[string]int{"a": 1, "b": 2, "c": 3}
 m2 := maps.Clone(m)
 m2["d"] = 4
 fmt.Println(len(m), len(m2))
 keys := slices.Sorted(maps.Keys(m))
 fmt.Println(keys)
 vals := slices.Sorted(maps.Values(m))
 fmt.Println(vals)
 fmt.Println(maps.Equal(m, map[string]int{"a":1,"b":2,"c":3}))
 maps.DeleteFunc(m2, func(k string, v int) bool { return v > 2 })
 ks := slices.Sorted(maps.Keys(m2))
 fmt.Println(ks)
 // iter.Seq with range-over-func
 sum := 0
 for v := range Count(5) { sum += v }
 fmt.Println("sum:", sum)
 // iter.Seq2
 for i, v := range Enumerate([]string{"a", "b", "c"}) {
  fmt.Printf("%d:%s ", i, v)
 }
 fmt.Println()
 // early break in range-over-func
 for v := range Count(100) {
  if v >= 3 { break }
  fmt.Print(v, " ")
 }
 fmt.Println()
 // slices.Collect from iterator
 collected := slices.Collect(Count(4))
 fmt.Println(collected)
}
