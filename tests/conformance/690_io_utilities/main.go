package main

// Regression: io utilities — MultiReader/TeeReader/LimitReader/SectionReader/Copy/
// CopyN/Pipe/MultiWriter/ReadFull/WriteString/EOF — all byte-exact vs go run.
import ("fmt";"io";"strings";"bytes";"sync")
func main(){
 // MultiReader
 mr := io.MultiReader(strings.NewReader("Hello, "), strings.NewReader("World"), strings.NewReader("!"))
 data, _ := io.ReadAll(mr)
 fmt.Printf("%q\n", data)
 // TeeReader
 var buf bytes.Buffer
 tr := io.TeeReader(strings.NewReader("teed data"), &buf)
 out, _ := io.ReadAll(tr)
 fmt.Printf("%q %q\n", out, buf.String())
 // LimitReader
 lr := io.LimitReader(strings.NewReader("0123456789"), 4)
 limited, _ := io.ReadAll(lr)
 fmt.Printf("%q\n", limited)
 // SectionReader
 sr := io.NewSectionReader(strings.NewReader("0123456789"), 3, 4)
 sect, _ := io.ReadAll(sr)
 fmt.Printf("%q\n", sect)
 // io.Copy + CopyN
 var dst bytes.Buffer
 n, _ := io.Copy(&dst, strings.NewReader("copy me"))
 fmt.Println(n, dst.String())
 var dst2 bytes.Buffer
 io.CopyN(&dst2, strings.NewReader("0123456789"), 5)
 fmt.Println(dst2.String())
 // io.Pipe with goroutine
 pr, pw := io.Pipe()
 var wg sync.WaitGroup
 wg.Add(1)
 go func(){ defer wg.Done(); pw.Write([]byte("piped message")); pw.Close() }()
 piped, _ := io.ReadAll(pr)
 wg.Wait()
 fmt.Printf("%q\n", piped)
 // MultiWriter
 var b1, b2 bytes.Buffer
 mw := io.MultiWriter(&b1, &b2)
 mw.Write([]byte("broadcast"))
 fmt.Printf("%q %q\n", b1.String(), b2.String())
 // ReadFull, ReadAtLeast
 r := strings.NewReader("exactly")
 buf2 := make([]byte, 7)
 io.ReadFull(r, buf2)
 fmt.Printf("%q\n", buf2)
 // WriteString
 var b3 bytes.Buffer
 io.WriteString(&b3, "written")
 fmt.Println(b3.String())
 // io.EOF
 r2 := strings.NewReader("")
 _, err := r2.Read(make([]byte, 1))
 fmt.Println(err == io.EOF)
}
