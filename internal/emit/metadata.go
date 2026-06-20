package emit

import (
	"encoding/binary"
	"unicode/utf16"
)

// heaps accumulates the four ECMA-335 metadata heaps. Indices are byte offsets
// (#Strings/#US/#Blob) or 1-based indices (#GUID), all uint32. The tables stream
// always references the #Strings/#GUID/#Blob heaps with 4-byte indices (HeapSizes
// = 0x07), so a heap may grow past 64 KiB — required for large programs (goja).
type heaps struct {
	strings   []byte
	stringMap map[string]uint32
	blobs     []byte
	us        []byte
	guids     []byte
}

func newHeaps() *heaps {
	return &heaps{
		strings:   []byte{0}, // offset 0 == empty string
		stringMap: map[string]uint32{"": 0},
		blobs:     []byte{0}, // offset 0 == empty blob
		us:        []byte{0}, // offset 0 == empty user string
	}
}

func (h *heaps) addString(s string) uint32 {
	if off, ok := h.stringMap[s]; ok {
		return off
	}
	off := uint32(len(h.strings))
	h.strings = append(h.strings, s...)
	h.strings = append(h.strings, 0)
	h.stringMap[s] = off
	return off
}

// addBlob appends a length-prefixed blob and returns its offset.
func (h *heaps) addBlob(data []byte) uint32 {
	if len(data) == 0 {
		return 0
	}
	off := uint32(len(h.blobs))
	h.blobs = appendCompressedUint(h.blobs, uint32(len(data)))
	h.blobs = append(h.blobs, data...)
	return off
}

// addUserString appends a UTF-16 user string (for ldstr) and returns its offset.
func (h *heaps) addUserString(s string) uint32 {
	off := uint32(len(h.us))
	u16 := utf16.Encode([]rune(s))
	raw := make([]byte, len(u16)*2+1)
	for i, c := range u16 {
		binary.LittleEndian.PutUint16(raw[i*2:], c)
	}
	// Trailing flag byte: 1 if any char needs special handling, else 0.
	raw[len(raw)-1] = userStringFinalByte(s)
	h.us = appendCompressedUint(h.us, uint32(len(raw)))
	h.us = append(h.us, raw...)
	return off
}

// addGUID appends a 16-byte GUID and returns its 1-based index.
func (h *heaps) addGUID(g [16]byte) uint32 {
	h.guids = append(h.guids, g[:]...)
	return uint32(len(h.guids) / 16)
}

func userStringFinalByte(s string) byte {
	for _, r := range s {
		if r > 0x7F {
			return 1
		}
		switch r {
		case 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
			0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17,
			0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E, 0x1F, 0x27, 0x2D, 0x7F:
			return 1
		}
	}
	return 0
}

// appendCompressedUint appends an ECMA-335 II.23.2 compressed unsigned integer.
func appendCompressedUint(b []byte, v uint32) []byte {
	switch {
	case v < 0x80:
		return append(b, byte(v))
	case v < 0x4000:
		return append(b, byte(0x80|(v>>8)), byte(v))
	default:
		return append(b, byte(0xC0|(v>>24)), byte(v>>16), byte(v>>8), byte(v))
	}
}

func align4(b []byte) []byte {
	for len(b)%4 != 0 {
		b = append(b, 0)
	}
	return b
}

// buildMetadata assembles the full metadata blob (root + stream headers + the
// five streams) from already-populated heaps and a serialized tables stream.
func buildMetadata(tables []byte, h *heaps) []byte {
	strings := align4(append([]byte{}, h.strings...))
	us := align4(append([]byte{}, h.us...))
	guids := align4(append([]byte{}, h.guids...))
	blobs := align4(append([]byte{}, h.blobs...))
	tbl := align4(append([]byte{}, tables...))

	const version = "v4.0.30319\x00\x00" // padded to 4-byte boundary, length 12
	type stream struct {
		name string
		data []byte
	}
	streams := []stream{
		{"#~", tbl},
		{"#Strings", strings},
		{"#US", us},
		{"#GUID", guids},
		{"#Blob", blobs},
	}

	// Compute the size of the root header so stream offsets are absolute.
	root := 4 + 2 + 2 + 4 + 4 + len(version) + 2 + 2 // sig..streams count
	headers := 0
	for _, s := range streams {
		headers += 8 + roundUp(len(s.name)+1, 4)
	}
	dataStart := root + headers

	var out []byte
	w := &writer{&out}
	w.u32(0x424A5342) // "BSJB"
	w.u16(1)          // major
	w.u16(1)          // minor
	w.u32(0)          // reserved
	w.u32(uint32(len(version)))
	w.bytes([]byte(version))
	w.u16(0)                    // flags
	w.u16(uint16(len(streams))) // number of streams

	offset := dataStart
	for _, s := range streams {
		w.u32(uint32(offset))
		w.u32(uint32(len(s.data)))
		nameBytes := append([]byte(s.name), 0)
		for len(nameBytes)%4 != 0 {
			nameBytes = append(nameBytes, 0)
		}
		w.bytes(nameBytes)
		offset += len(s.data)
	}
	for _, s := range streams {
		w.bytes(s.data)
	}
	return out
}

func roundUp(n, a int) int { return (n + a - 1) / a * a }

// writer is a tiny little-endian byte-buffer helper.
type writer struct{ b *[]byte }

func (w *writer) u8(v byte)    { *w.b = append(*w.b, v) }
func (w *writer) u16(v uint16) { *w.b = binary.LittleEndian.AppendUint16(*w.b, v) }
func (w *writer) u32(v uint32) { *w.b = binary.LittleEndian.AppendUint32(*w.b, v) }

// heap writes a #Strings/#GUID/#Blob heap index. HeapSizes is 0x07, so every heap
// reference in the tables stream is 4 bytes (programs may exceed a 64 KiB heap).
func (w *writer) heap(v uint32)  { *w.b = binary.LittleEndian.AppendUint32(*w.b, v) }
func (w *writer) u64(v uint64)   { *w.b = binary.LittleEndian.AppendUint64(*w.b, v) }
func (w *writer) bytes(p []byte) { *w.b = append(*w.b, p...) }
