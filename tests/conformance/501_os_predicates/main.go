package main
import ("fmt";"os";"errors")
func main(){
 fmt.Println("pagesize",os.Getpagesize())
 fmt.Println("pathsep",os.IsPathSeparator('/'),os.IsPathSeparator('x'))
 fmt.Println("isexist",os.IsExist(os.ErrExist),os.IsExist(errors.New("x")),os.IsExist(nil))
 fmt.Println("istimeout",os.IsTimeout(os.ErrDeadlineExceeded),os.IsTimeout(errors.New("x")))
 os.Setenv("ZZ","1"); os.Clearenv(); fmt.Println("clearenv",os.Getenv("ZZ")=="")
}
