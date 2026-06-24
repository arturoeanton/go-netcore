package main
import ("fmt";"os";"errors")
func main(){
 os.Setenv("FOO","bar"); os.Setenv("BAZ","qux")
 fmt.Println(os.ExpandEnv("$FOO and ${BAZ} and $UNDEF end"))
 fmt.Println(os.ExpandEnv("price is $$5 and a=${FOO}x"))
 fmt.Println(os.Expand("a=$A b=${B}", func(k string) string { return "<"+k+">" }))
 fmt.Println(os.Expand("$ at end of $", func(k string) string { return "X" }))
 fmt.Println(os.IsPermission(errors.New("open /x: permission denied")), os.IsPermission(os.ErrPermission))
 se:=os.NewSyscallError("read",errors.New("boom"))
 fmt.Println(se.Error(), os.NewSyscallError("x",nil)==nil)
 h,err:=os.Hostname(); fmt.Println("hostok:", len(h)>0, err)
}
