//go:build goclr

package mixed

import "testing"

func TestAddOK(t *testing.T) {
	if Add(2, 3) != 5 {
		t.Errorf("Add(2,3) = %d, want 5", Add(2, 3))
	}
}

func TestMulFail(t *testing.T) {
	if Mul(2, 3) != 7 { // wrong on purpose
		t.Errorf("Mul(2,3) = %d, want 7", Mul(2, 3))
	}
}

func TestFatalStops(t *testing.T) {
	t.Fatalf("stopping with %d", 42)
	t.Log("must not run")
}

func TestSkipped(t *testing.T) {
	t.Skip("skipping this one")
}

func TestSub(t *testing.T) {
	t.Run("good", func(t *testing.T) {
		if Add(1, 1) != 2 {
			t.Errorf("nope")
		}
	})
	t.Run("bad", func(t *testing.T) {
		t.Errorf("subtest failed")
	})
}
