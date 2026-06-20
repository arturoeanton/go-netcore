package goir

import "testing"

func TestTypeConstructors(t *testing.T) {
	sl := SliceType(TInt64)
	if sl.Kind != KSlice {
		t.Errorf("SliceType kind = %v, want KSlice", sl.Kind)
	}
	if sl.Elem == nil || sl.Elem.Kind != KInt64 {
		t.Errorf("SliceType elem = %+v, want int64", sl.Elem)
	}

	pt := PtrType(TString)
	if pt.Kind != KPtr {
		t.Errorf("PtrType kind = %v, want KPtr", pt.Kind)
	}
	if pt.Elem == nil || pt.Elem.Kind != KString {
		t.Errorf("PtrType elem = %+v, want string", pt.Elem)
	}

	// Nesting: *[]int.
	nested := PtrType(SliceType(TInt64))
	if nested.Kind != KPtr || nested.Elem.Kind != KSlice || nested.Elem.Elem.Kind != KInt64 {
		t.Errorf("nested *[]int built incorrectly: %+v", nested)
	}

	s := &Struct{Name: "Point"}
	st := StructType(s)
	if st.Kind != KStruct || st.Struct != s {
		t.Errorf("StructType = %+v, want struct of Point", st)
	}
}

func TestSliceTypeElemIsCopy(t *testing.T) {
	// Each SliceType call must capture an independent Elem so two slices of
	// different element types do not alias the same pointer.
	a := SliceType(TInt64)
	b := SliceType(TString)
	if a.Elem == b.Elem {
		t.Fatal("distinct SliceType calls share the same Elem pointer")
	}
	if a.Elem.Kind != KInt64 || b.Elem.Kind != KString {
		t.Errorf("elem types aliased: a=%v b=%v", a.Elem.Kind, b.Elem.Kind)
	}
}

func TestStructFieldIndex(t *testing.T) {
	s := &Struct{
		Name: "User",
		Fields: []Field{
			{Name: "ID", Type: TInt64},
			{Name: "Name", Type: TString},
			{Name: "Active", Type: TBool},
		},
	}
	cases := map[string]int{"ID": 0, "Name": 1, "Active": 2, "Missing": -1, "": -1}
	for name, want := range cases {
		if got := s.FieldIndex(name); got != want {
			t.Errorf("FieldIndex(%q) = %d, want %d", name, got, want)
		}
	}
}
