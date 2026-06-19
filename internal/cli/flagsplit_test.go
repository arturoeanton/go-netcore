package cli

import (
	"reflect"
	"testing"
)

func TestSplitArgs(t *testing.T) {
	vf := buildValueFlags
	cases := []struct {
		name     string
		args     []string
		wantFlag []string
		wantPos  []string
	}{
		{
			name:     "flags after positional (spec syntax)",
			args:     []string{"./cmd/server", "-o", "bin/server.dll"},
			wantFlag: []string{"-o", "bin/server.dll"},
			wantPos:  []string{"./cmd/server"},
		},
		{
			name:     "flags before positional",
			args:     []string{"-o", "x.dll", "--verbose", "./cmd/server"},
			wantFlag: []string{"-o", "x.dll", "--verbose"},
			wantPos:  []string{"./cmd/server"},
		},
		{
			name:     "equals form",
			args:     []string{"./pkg", "-profile=echo-goja"},
			wantFlag: []string{"-profile=echo-goja"},
			wantPos:  []string{"./pkg"},
		},
		{
			name:     "double dash terminator",
			args:     []string{"-o", "x", "--", "-not-a-flag"},
			wantFlag: []string{"-o", "x"},
			wantPos:  []string{"-not-a-flag"},
		},
	}
	for _, tc := range cases {
		t.Run(tc.name, func(t *testing.T) {
			flags, pos := splitArgs(tc.args, vf)
			if !reflect.DeepEqual(flags, tc.wantFlag) {
				t.Errorf("flags = %v, want %v", flags, tc.wantFlag)
			}
			if !reflect.DeepEqual(pos, tc.wantPos) {
				t.Errorf("positionals = %v, want %v", pos, tc.wantPos)
			}
		})
	}
}
