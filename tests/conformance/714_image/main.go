package main

import (
	"bytes"
	"fmt"
	"image"
	"image/color"
)

// image is compiled from real source (depends only on image/color plus shimmed
// math/bits/strconv/bufio/sync): the Rectangle/Point geometry, the buffered image
// types (RGBA/NRGBA/Gray/Paletted/…), SubImage, the Palette, and the decoder
// registry (ErrFormat with no registered decoder) must all match go run.
func main() {
	// Rectangle / Point geometry.
	r := image.Rect(0, 0, 100, 50)
	fmt.Println(r, r.Dx(), r.Dy(), r.Empty())
	fmt.Println(r.Intersect(image.Rect(50, 25, 150, 75)))
	fmt.Println(image.Rect(0, 0, 10, 10).Union(image.Rect(5, 5, 20, 20)))
	fmt.Println(r.Overlaps(image.Rect(50, 25, 150, 75)), r.In(image.Rect(-10, -10, 200, 200)))
	fmt.Println(image.Pt(3, 4).Add(image.Pt(1, 1)), image.Pt(5, 5).Mul(2), image.Pt(10, 8).Div(2))
	fmt.Println(image.Pt(3, 4).In(r), image.Pt(3, 4).Eq(image.Pt(3, 4)))
	fmt.Println(image.Rect(5, 5, 0, 0)) // canonicalized to (0,0)-(5,5)
	fmt.Println(image.ZP, image.ZR)

	// RGBA buffer: Set/At, PixOffset, Stride, SubImage.
	img := image.NewRGBA(image.Rect(0, 0, 8, 8))
	for y := 0; y < 8; y++ {
		for x := 0; x < 8; x++ {
			img.Set(x, y, color.RGBA{uint8(x * 32), uint8(y * 32), 0, 255})
		}
	}
	fmt.Println(img.RGBAAt(3, 4))
	fmt.Println(img.At(1, 1).RGBA())
	fmt.Println(img.PixOffset(3, 4), len(img.Pix), img.Stride, img.Bounds())
	sub := img.SubImage(image.Rect(2, 2, 6, 6)).(*image.RGBA)
	fmt.Println(sub.Bounds(), sub.RGBAAt(3, 4))

	// NRGBA / Gray.
	n := image.NewNRGBA(image.Rect(0, 0, 2, 2))
	n.Set(0, 0, color.NRGBA{100, 100, 100, 200})
	fmt.Println(n.NRGBAAt(0, 0))
	g := image.NewGray(image.Rect(0, 0, 2, 2))
	g.Set(0, 0, color.Gray{200})
	fmt.Println(g.GrayAt(0, 0))

	// Palette + Paletted.
	pal := color.Palette{color.Black, color.White, color.RGBA{255, 0, 0, 255}}
	fmt.Println(pal.Index(color.RGBA{200, 10, 10, 255}))
	fmt.Println(pal.Convert(color.RGBA{200, 10, 10, 255}))
	p := image.NewPaletted(image.Rect(0, 0, 2, 2), pal)
	p.SetColorIndex(0, 0, 2)
	fmt.Println(p.ColorIndexAt(0, 0), p.At(0, 0))

	// Decoder registry with no registered format -> ErrFormat.
	_, fn, err := image.Decode(bytes.NewReader([]byte("notanimage")))
	fmt.Println(fn, err, err == image.ErrFormat)
	_, fn2, err2 := image.DecodeConfig(bytes.NewReader([]byte("xyz")))
	fmt.Println(fn2, err2)
}
