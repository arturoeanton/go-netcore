package main
import ("fmt";"strconv")
func main(){
	for _,s:=range []string{"42","-7","9223372036854775808","-9223372036854775809","+5","0x1f","abc",""} {
		v,e:=strconv.Atoi(s); fmt.Printf("Atoi(%q)=%d err=%v\n",s,v,e)
	}
	for _,s:=range []string{"0xff","0b101","0o17","z","128","-128"} {
		v,e:=strconv.ParseInt(s,0,64); fmt.Printf("ParseInt(%q,0)=%d err=%v\n",s,v,e)
	}
	v,e:=strconv.ParseInt("128",10,8); fmt.Printf("ParseInt(128,10,8)=%d err=%v\n",v,e)
	v2,e2:=strconv.ParseUint("+5",10,64); fmt.Printf("ParseUint(+5)=%d err=%v\n",v2,e2)
	v3,e3:=strconv.ParseUint("18446744073709551615",10,64); fmt.Printf("ParseUint(max)=%d err=%v\n",v3,e3)
	for _,s:=range []string{"inf","-Inf","NaN","1e999","0x1p4","1_000.5","3.14"} {
		f,e:=strconv.ParseFloat(s,64); fmt.Printf("ParseFloat(%q)=%v err=%v\n",s,f,e)
	}
	fmt.Println(strconv.FormatFloat(1.5,'e',-1,64), strconv.FormatFloat(0,'f',-1,64), strconv.FormatFloat(1e20,'g',3,64))
	fmt.Println(strconv.QuoteToASCII("café\n"))
}
