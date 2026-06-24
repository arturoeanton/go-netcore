package main
import ("fmt";"mime/multipart";"bytes";"strings")
func main(){
 fmt.Printf("%q\n",multipart.FileContentDisposition("field","file.txt"))
 fmt.Printf("%q\n",multipart.FileContentDisposition(`na"me`,"a/b\\c.txt"))
 var b bytes.Buffer
 w:=multipart.NewWriter(&b)
 fw,_:=w.CreateFormField("name"); fmt.Fprint(fw,"value")
 ff,_:=w.CreateFormFile("upload","doc.pdf"); fmt.Fprint(ff,"data")
 w.Close()
 out:=b.String()
 out=strings.ReplaceAll(out,w.Boundary(),"BOUNDARY")
 fmt.Print(out)
 fmt.Println("err:",multipart.ErrMessageTooLarge)
}
