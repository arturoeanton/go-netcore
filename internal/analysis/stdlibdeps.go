package analysis

// OverlayStatus classifies how the GoCLR stdlib overlay handles a standard
// library import path.
type OverlayStatus int

const (
	// OverlayFull: the overlay provides the API surface goclr needs.
	OverlayFull OverlayStatus = iota
	// OverlayPartial: provided but a documented subset only (e.g. reflect).
	OverlayPartial
	// OverlayNone: not provided by the overlay yet.
	OverlayNone
)

// stdlibOverlay maps stdlib import paths to their overlay status for the
// echo-goja profile (spec §22). Packages absent from this map default to
// OverlayNone and produce a "stdlib missing" diagnostic when imported.
var stdlibOverlay = map[string]OverlayStatus{
	"errors":          OverlayFull,
	"fmt":             OverlayFull,
	"strconv":         OverlayFull,
	"strings":         OverlayFull,
	"bytes":           OverlayFull,
	"io":              OverlayFull,
	"sort":            OverlayFull,
	"math":            OverlayFull,
	"math/bits":       OverlayFull,
	"time":            OverlayPartial,
	"sync":            OverlayFull,
	"sync/atomic":     OverlayFull,
	"encoding/json":   OverlayFull,
	"encoding/base64": OverlayFull,
	"encoding/hex":    OverlayFull,
	"net/http":        OverlayFull,
	"net/url":         OverlayFull,
	"net/textproto":   OverlayPartial,
	"mime":            OverlayPartial,
	"mime/multipart":  OverlayPartial,
	"regexp":          OverlayPartial,
	"regexp/syntax":   OverlayPartial,
	"unicode":         OverlayFull,
	"unicode/utf8":    OverlayFull,
	"unicode/utf16":   OverlayFull,
	"reflect":         OverlayPartial,
	"context":         OverlayFull,
	"runtime":         OverlayPartial,
	"log":             OverlayFull,
	"log/slog":        OverlayPartial,
	"path":            OverlayFull,
	"path/filepath":   OverlayPartial,
	"os":              OverlayPartial,
	"hash":            OverlayFull,
	"hash/crc32":      OverlayFull,
	"hash/fnv":        OverlayFull,
	"crypto/rand":     OverlayPartial,
	"crypto/sha1":     OverlayFull,
	"crypto/sha256":   OverlayFull,
	"crypto/subtle":   OverlayFull,
	"bufio":           OverlayFull,
	"container/list":  OverlayFull,
	"container/heap":  OverlayFull,
	"slices":          OverlayPartial,
	"cmp":             OverlayFull,
	// builtin pseudo-packages and always-available primitives
	"unsafe": OverlayPartial,
}

// LookupOverlay returns the overlay status for a stdlib import path.
func LookupOverlay(importPath string) (OverlayStatus, bool) {
	s, ok := stdlibOverlay[importPath]
	return s, ok
}
