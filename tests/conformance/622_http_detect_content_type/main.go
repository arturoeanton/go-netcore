package main

import (
	"fmt"
	"net/http"
)

// http.DetectContentType ports net/http's MIME-sniffing algorithm: consider the first
// 512 bytes, run the ordered signature table (HTML tags, XML, PDF/PS, BOMs, images,
// audio/video, archives), else fall back to text/plain vs application/octet-stream.
func main() {
	cases := []struct {
		name string
		data []byte
	}{
		{"html", []byte("<html><body>")},
		{"doctype", []byte("<!DOCTYPE HTML><html>")},
		{"html-leading-ws", []byte("\n\n  <HTML>")},
		{"comment", []byte("<!-- hi -->")},
		{"a-tag", []byte("<a href")},
		{"not-a-tag", []byte("<april fools")},
		{"xml", []byte("<?xml version=\"1.0\"?>")},
		{"xml-ws", []byte("  <?xml")},
		{"pdf", []byte("%PDF-1.7")},
		{"postscript", []byte("%!PS-Adobe-3.0")},
		{"png", []byte("\x89PNG\r\n\x1a\n")},
		{"jpeg", []byte("\xFF\xD8\xFF\xE0")},
		{"gif87", []byte("GIF87a")},
		{"gif89", []byte("GIF89a")},
		{"bmp", []byte("BM\x00\x00")},
		{"ico", []byte("\x00\x00\x01\x00")},
		{"webp", []byte("RIFF\x00\x00\x00\x00WEBPVP8 ")},
		{"wave", []byte("RIFF\x00\x00\x00\x00WAVEfmt ")},
		{"avi", []byte("RIFF\x00\x00\x00\x00AVI LIST")},
		{"ogg", []byte("OggS\x00")},
		{"midi", []byte("MThd\x00\x00\x00\x06")},
		{"id3-mp3", []byte("ID3\x03\x00")},
		{"webm", []byte("\x1A\x45\xDF\xA3")},
		{"gzip", []byte("\x1F\x8B\x08\x00")},
		{"zip", []byte("PK\x03\x04")},
		{"rar-real", []byte("Rar!\x1A\x07\x00")},
		{"rar-v5", []byte("Rar!\x1A\x07\x01\x00")},
		{"rar-bad", []byte("Rar \x1A\x07\x00")}, // wrong magic byte -> not rar
		{"wasm", []byte("\x00asm\x01\x00\x00\x00")},
		{"utf16be", []byte("\xFE\xFFhi")},
		{"utf16le", []byte("\xFF\xFEhi")},
		{"utf8bom", []byte("\xEF\xBB\xBFhi")},
		{"plain-text", []byte("just plain text here")},
		{"text-leading-ws", []byte("   hello")},
		{"binary", []byte("\x00\x01\x02\x03\x04\x05")},
		{"empty", []byte("")},
		{"mp4", append([]byte{0, 0, 0, 0x18}, []byte("ftypmp42")...)},
	}
	for _, c := range cases {
		fmt.Printf("%-16s %s\n", c.name, http.DetectContentType(c.data))
	}
}
