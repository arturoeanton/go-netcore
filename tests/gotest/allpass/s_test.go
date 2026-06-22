//go:build goclr

package allpass

import "testing"

func TestDouble(t *testing.T) {
	if Double(21) != 42 {
		t.Fatalf("Double(21) = %d", Double(21))
	}
}

func TestDoubleZero(t *testing.T) {
	if Double(0) != 0 {
		t.Errorf("Double(0) != 0")
	}
}
