package main

import (
	"fmt"
	"image"
	"image/color"
	"image/draw"
)

// image/draw is compiled from real source (depends on image + image/color +
// image/internal/imageutil): Draw/DrawMask with the Src and Over operators, a
// Uniform source, alpha masks, and the alpha-blending math must match go run.
func main() {
	dst := image.NewRGBA(image.Rect(0, 0, 6, 6))
	draw.Draw(dst, dst.Bounds(), &image.Uniform{color.RGBA{50, 50, 50, 255}}, image.Point{}, draw.Src)
	fmt.Println(dst.RGBAAt(0, 0), dst.RGBAAt(5, 5))

	// Src copy of a solid color into a sub-rectangle.
	blue := image.NewRGBA(image.Rect(0, 0, 6, 6))
	draw.Draw(blue, blue.Bounds(), &image.Uniform{color.RGBA{0, 0, 255, 255}}, image.Point{}, draw.Src)
	draw.Draw(dst, image.Rect(1, 1, 3, 3), blue, image.Point{}, draw.Src)
	fmt.Println(dst.RGBAAt(1, 1), dst.RGBAAt(0, 0))

	// Over with a semi-transparent overlay (alpha blending).
	overlay := image.NewRGBA(image.Rect(0, 0, 6, 6))
	draw.Draw(overlay, overlay.Bounds(), &image.Uniform{color.RGBA{200, 0, 0, 128}}, image.Point{}, draw.Src)
	draw.Draw(dst, image.Rect(3, 3, 6, 6), overlay, image.Pt(3, 3), draw.Over)
	fmt.Println(dst.RGBAAt(3, 3), dst.RGBAAt(5, 5))

	// DrawMask with a graduated alpha mask.
	mask := image.NewAlpha(image.Rect(0, 0, 6, 6))
	for x := 0; x < 6; x++ {
		mask.SetAlpha(x, 0, color.Alpha{uint8(x * 40)})
	}
	draw.DrawMask(dst, image.Rect(0, 0, 6, 1), &image.Uniform{color.RGBA{0, 255, 0, 255}}, image.Point{}, mask, image.Point{}, draw.Over)
	fmt.Println(dst.RGBAAt(0, 0), dst.RGBAAt(3, 0), dst.RGBAAt(5, 0))

	// Drawing onto a Gray destination.
	gd := image.NewGray(image.Rect(0, 0, 3, 3))
	draw.Draw(gd, gd.Bounds(), &image.Uniform{color.Gray{128}}, image.Point{}, draw.Src)
	fmt.Println(gd.GrayAt(1, 1))

	// The Drawer / Quantizer / Op surface.
	var d draw.Drawer = draw.Src
	d.Draw(gd, gd.Bounds(), &image.Uniform{color.Gray{64}}, image.Point{})
	fmt.Println(gd.GrayAt(0, 0))
	fmt.Println(draw.FloydSteinberg != nil)
	fmt.Println(draw.Over, draw.Src)
}
