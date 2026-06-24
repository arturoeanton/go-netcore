package main
import ("bytes";"encoding/xml";"fmt")
func main(){
 var b bytes.Buffer
 xml.EscapeText(&b,[]byte("a<b>&'\"\tc"));fmt.Println(b.String())
 cd:=xml.CharData("hello");cp:=cd.Copy();fmt.Println(string(cp))
 cm:=xml.Comment("note");fmt.Println(string(cm.Copy()))
 se:=xml.StartElement{Name:xml.Name{Local:"item"},Attr:[]xml.Attr{{Name:xml.Name{Local:"id"},Value:"7"}}}
 se2:=se.Copy();fmt.Println(se2.Name.Local,se2.Attr[0].Value)
 end:=se.End();fmt.Println(end.Name.Local)
 fmt.Println(xml.HTMLAutoClose[:3])
 tok:=xml.CopyToken(se).(xml.StartElement);fmt.Println(tok.Name.Local)
}
