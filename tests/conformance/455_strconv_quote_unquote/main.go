package main
import ("fmt";"strconv")
func main(){
 fmt.Println(strconv.QuoteRune('A'),strconv.QuoteRune('\n'),strconv.QuoteRune('\''))
 fmt.Println(strconv.QuoteRuneToASCII('世'),strconv.QuoteRune('世'))
 u,e:=strconv.Unquote(`"hello\tworld\n"`);fmt.Printf("%q %v\n",u,e)
 u2,_:=strconv.Unquote("`raw\\nstring`");fmt.Printf("%q\n",u2)
 u3,_:=strconv.Unquote(`'A'`);fmt.Printf("%q\n",u3)
 u4,_:=strconv.Unquote(`"世\x41"`);fmt.Printf("%q\n",u4)
 _,be:=strconv.Unquote(`"bad`);fmt.Println(be!=nil)
 v,mb,tail,_:=strconv.UnquoteChar(`\n123`,'"');fmt.Println(v,mb,tail)
 p,_:=strconv.QuotedPrefix(`"abc"def`);fmt.Printf("%q\n",p)
 _,pe:=strconv.Atoi("notanum");var ne *strconv.NumError;if e2,ok:=pe.(*strconv.NumError);ok{ne=e2};fmt.Println(ne.Error(),ne.Unwrap()==strconv.ErrSyntax)
 fmt.Println(strconv.FormatComplex(complex(1.5,-2),'f',-1,128))
 c,_:=strconv.ParseComplex("(3+4i)",128);fmt.Println(c)
 c2,_:=strconv.ParseComplex("5i",128);fmt.Println(c2)
 fmt.Println(string(strconv.AppendQuoteRune([]byte("x="),'Z')))
}
