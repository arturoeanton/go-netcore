package main
import ("fmt";"mime")
func main(){
 fmt.Printf("%q\n",mime.FormatMediaType("text/html",map[string]string{"charset":"utf-8"}))
 fmt.Printf("%q\n",mime.FormatMediaType("Text/HTML",map[string]string{"Charset":"UTF-8","boundary":"x"}))
 fmt.Printf("%q\n",mime.FormatMediaType("application/json",nil))
 fmt.Printf("%q\n",mime.FormatMediaType("form-data",map[string]string{"name":"file","filename":"a b.txt"}))
 fmt.Printf("%q\n",mime.FormatMediaType("text/plain",map[string]string{"v":"with\"quote"}))
 fmt.Printf("%q\n",mime.FormatMediaType("bad type",nil))
 fmt.Printf("%q\n",mime.FormatMediaType("text/plain",map[string]string{"x":"héllo"}))
 fmt.Printf("%v\n",mime.AddExtensionType(".foo","text/foo"))
 fmt.Printf("%q\n",mime.TypeByExtension(".foo"))
 fmt.Printf("%v\n",mime.AddExtensionType("bar","text/bar"))
 fmt.Printf("%v\n",mime.AddExtensionType("",""))
 fmt.Printf("%v\n",mime.AddExtensionType(".baz","not a type"))
 fmt.Println(mime.ErrInvalidMediaParameter)
}
