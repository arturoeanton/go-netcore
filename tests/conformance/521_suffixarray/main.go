package main
import ("fmt";"index/suffixarray";"regexp")
func main(){
 data:=[]byte("the quick brown fox jumps over the lazy dog, the fox runs")
 idx:=suffixarray.New(data)
 fmt.Printf("bytes=%q\n",idx.Bytes())
 for _,pat:=range []string{"the","fox","o","[a-z]+","th.","xyz","t.e","[0-9]+","\\bthe\\b"}{
  re:=regexp.MustCompile(pat)
  all:=idx.FindAllIndex(re,-1)
  fmt.Printf("%q all=%v match=%v\n",pat,all,fmt.Sprint(all)==fmt.Sprint(re.FindAllIndex(data,-1)))
 }
 // n>0 with non-literal regex (no literal prefix -> Go uses regexp path, byte-exact)
 fmt.Println("alpha2",idx.FindAllIndex(regexp.MustCompile("[a-z]+"),3))
}
