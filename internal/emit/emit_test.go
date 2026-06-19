package emit

import (
	"bytes"
	"debug/pe"
	"os"
	"path/filepath"
	"testing"

	"github.com/arturoeanton/go-netcore/internal/goir"
)

// sampleProgram builds a main that prints a string and a boxed int, a method
// with locals, and a struct-returning method — exercising arrays, boxing, calls,
// StandAloneSig, and struct TypeDef/Field metadata.
func sampleProgram() *goir.Program {
	pt := &goir.Struct{Name: "Point", GoName: "Point", Fields: []goir.Field{
		{Name: "X", Type: goir.TInt64}, {Name: "Y", Type: goir.TInt64},
	}}
	mk := &goir.Method{
		Name: "mk", GoName: "mk", Ret: goir.StructType(pt),
		Locals: []goir.Type{goir.StructType(pt)},
		Code: []goir.Op{
			{Code: goir.OpLdLocA, Local: 0},
			{Code: goir.OpInitObj, Struct: pt},
			{Code: goir.OpLdLocA, Local: 0},
			{Code: goir.OpLdcI8, Int: 7},
			{Code: goir.OpStFld, Struct: pt, Field: 0},
			{Code: goir.OpLdLoc, Local: 0},
			{Code: goir.OpRet},
		},
	}
	add := &goir.Method{
		Name: "add", GoName: "add",
		Params: []goir.Type{goir.TInt64, goir.TInt64}, Ret: goir.TInt64,
		Locals: []goir.Type{goir.TInt64},
		Code: []goir.Op{
			{Code: goir.OpLdArg, Arg: 0},
			{Code: goir.OpLdArg, Arg: 1},
			{Code: goir.OpAdd},
			{Code: goir.OpStLoc, Local: 0},
			{Code: goir.OpLdLoc, Local: 0},
			{Code: goir.OpRet},
		},
	}
	main := &goir.Method{
		Name: "main", GoName: "main", Ret: goir.TVoid,
		Code: []goir.Op{
			// println("hello clr")
			{Code: goir.OpLdcI4, Int: 1},
			{Code: goir.OpNewObjArray},
			{Code: goir.OpDup},
			{Code: goir.OpLdcI4, Int: 0},
			{Code: goir.OpLdStr, Str: "hello clr"},
			{Code: goir.OpStelemRef},
			{Code: goir.OpCallPrintln},
			// println(add(2,3))
			{Code: goir.OpLdcI4, Int: 1},
			{Code: goir.OpNewObjArray},
			{Code: goir.OpDup},
			{Code: goir.OpLdcI4, Int: 0},
			{Code: goir.OpLdcI8, Int: 2},
			{Code: goir.OpLdcI8, Int: 3},
			{Code: goir.OpCallMethod, Callee: add},
			{Code: goir.OpBox, BoxTy: goir.TInt64},
			{Code: goir.OpStelemRef},
			{Code: goir.OpCallPrintln},
			{Code: goir.OpRet},
		},
	}
	return &goir.Program{
		AssemblyName: "sample",
		Structs:      []*goir.Struct{pt},
		Methods:      []*goir.Method{main, add, mk},
		Entry:        main,
	}
}

func TestEmitProducesValidPE(t *testing.T) {
	out := filepath.Join(t.TempDir(), "sample.dll")
	if err := Emit(sampleProgram(), out); err != nil {
		t.Fatalf("Emit: %v", err)
	}
	f, err := pe.Open(out)
	if err != nil {
		t.Fatalf("debug/pe could not parse the emitted assembly: %v", err)
	}
	defer f.Close()

	if f.Machine != pe.IMAGE_FILE_MACHINE_I386 {
		t.Errorf("Machine = %#x, want I386", f.Machine)
	}
	if f.Section(".text") == nil {
		t.Error("missing .text section")
	}
	oh, ok := f.OptionalHeader.(*pe.OptionalHeader32)
	if !ok {
		t.Fatalf("OptionalHeader is %T, want *pe.OptionalHeader32", f.OptionalHeader)
	}
	const clrDir = 14
	if oh.DataDirectory[clrDir].VirtualAddress == 0 {
		t.Error("CLR runtime header data directory is empty")
	}
	if oh.DataDirectory[clrDir].Size != cliHeaderSize {
		t.Errorf("CLI header size = %d, want %d", oh.DataDirectory[clrDir].Size, cliHeaderSize)
	}
}

func TestEmitDeterministic(t *testing.T) {
	dir := t.TempDir()
	a := filepath.Join(dir, "a.dll")
	b := filepath.Join(dir, "b.dll")
	if err := Emit(sampleProgram(), a); err != nil {
		t.Fatal(err)
	}
	if err := Emit(sampleProgram(), b); err != nil {
		t.Fatal(err)
	}
	ba, _ := os.ReadFile(a)
	bb, _ := os.ReadFile(b)
	if !bytes.Equal(ba, bb) {
		t.Error("emission is not deterministic")
	}
}

func TestMethodBodyHeaderSelection(t *testing.T) {
	if methodBody(make([]byte, 10), 8, 0, nil)[0]&0x03 != 0x02 {
		t.Error("small bodiless-locals method should use tiny header")
	}
	if methodBody(make([]byte, 100), 8, 0, nil)[0]&0x07 != 0x03 {
		t.Error("large method should use fat header")
	}
	if methodBody(make([]byte, 10), 8, sigBase+1, nil)[0]&0x07 != 0x03 {
		t.Error("method with locals must use fat header to carry LocalVarSigTok")
	}
}
