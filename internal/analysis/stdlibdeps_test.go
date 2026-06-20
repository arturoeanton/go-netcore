package analysis

import "testing"

func TestLookupOverlay(t *testing.T) {
	cases := []struct {
		path  string
		want  OverlayStatus
		known bool
	}{
		{"fmt", OverlayFull, true},
		{"reflect", OverlayPartial, true},
		{"time", OverlayPartial, true},
		{"net/http", OverlayFull, true},
		{"database/sql", OverlayNone, false}, // not in the map => unknown
		{"some/random/pkg", OverlayNone, false},
	}
	for _, c := range cases {
		got, known := LookupOverlay(c.path)
		if known != c.known {
			t.Errorf("LookupOverlay(%q) known = %v, want %v", c.path, known, c.known)
			continue
		}
		if known && got != c.want {
			t.Errorf("LookupOverlay(%q) = %v, want %v", c.path, got, c.want)
		}
	}
}

// Every entry in the overlay map must carry one of the three defined statuses;
// this guards against a typo'd/zero status slipping in.
func TestOverlayStatusesValid(t *testing.T) {
	for path, st := range stdlibOverlay {
		if st != OverlayFull && st != OverlayPartial && st != OverlayNone {
			t.Errorf("%q has invalid overlay status %d", path, st)
		}
	}
}
