package main
import ("bytes";"compress/zlib";"fmt";"io";"os/signal";"syscall";"context")
func main(){
 var buf bytes.Buffer
 w,err:=zlib.NewWriterLevel(&buf,zlib.BestCompression); fmt.Println("err:",err)
 w.Write([]byte("hello zlib")); w.Close()
 r,_:=zlib.NewReader(&buf); out,_:=io.ReadAll(r); fmt.Printf("rt=%q\n",out)
 _,e:=zlib.NewWriterLevel(&buf,99); fmt.Println("bad:",e)
 var b2 bytes.Buffer
 wd,_:=zlib.NewWriterLevelDict(&b2,6,[]byte("dict")); wd.Write([]byte("xy")); wd.Close()
 rd,_:=zlib.NewReaderDict(&b2,[]byte("dict")); o2,_:=io.ReadAll(rd); fmt.Printf("dict=%q\n",o2)
 var b3 bytes.Buffer; w.Reset(&b3); w.Write([]byte("reset")); w.Close()
 r3,_:=zlib.NewReader(&b3); o3,_:=io.ReadAll(r3); fmt.Printf("reset=%q\n",o3)
 // signal
 fmt.Println("ignored:",signal.Ignored(syscall.SIGINT))
 ctx,stop:=signal.NotifyContext(context.Background(),syscall.SIGINT)
 fmt.Println("ctx err before:",ctx.Err()); stop(); fmt.Println("ctx err after stop:",ctx.Err())
}
