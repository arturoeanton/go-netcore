package main
import ("bytes";"encoding/json";"fmt")
func main(){
 src:=[]byte(`{"a": 1,  "b": [2, 3],  "c": {} }`)
 var c bytes.Buffer;json.Compact(&c,src);fmt.Println(c.String())
 var ind bytes.Buffer;json.Indent(&ind,src,"","  ");fmt.Println(ind.String())
 var h bytes.Buffer;json.HTMLEscape(&h,[]byte(`{"x":"<b>&</b>"}`));fmt.Println(h.String())
 // Delim.String directly
 fmt.Println(json.Delim('[').String(),json.Delim('}').String())
 // RawMessage.MarshalJSON directly
 rm:=json.RawMessage(`{"k":1}`);b,_:=rm.MarshalJSON();fmt.Println(string(b))
 var empty json.RawMessage;eb,_:=empty.MarshalJSON();fmt.Println(string(eb))
}
