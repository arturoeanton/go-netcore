package main

import (
	"fmt"
	"image/color"
)

// image/color is compiled from real source (a leaf package, pure arithmetic): the
// RGBA() methods of every color type, the Model conversions, and the YCbCr/CMYK
// conversion helpers must all match go run byte-for-byte.
func main() {
	// RGBA() of each concrete type (returns alpha-premultiplied 16-bit components).
	fmt.Println(color.RGBA{255, 128, 0, 255}.RGBA())
	fmt.Println(color.RGBA{255, 128, 0, 128}.RGBA())
	fmt.Println(color.NRGBA{100, 150, 200, 128}.RGBA())
	fmt.Println(color.NRGBA{100, 150, 200, 255}.RGBA())
	fmt.Println(color.Gray{200}.RGBA())
	fmt.Println(color.Gray16{40000}.RGBA())
	fmt.Println(color.Alpha{128}.RGBA())
	fmt.Println(color.Alpha16{30000}.RGBA())
	fmt.Println(color.RGBA64{65535, 32768, 0, 65535}.RGBA())
	fmt.Println(color.NRGBA64{40000, 50000, 60000, 32768}.RGBA())
	fmt.Println(color.CMYK{50, 100, 150, 200}.RGBA())

	// Model conversions.
	fmt.Println(color.GrayModel.Convert(color.RGBA{255, 128, 0, 255}))
	fmt.Println(color.Gray16Model.Convert(color.RGBA{255, 128, 0, 255}))
	fmt.Println(color.RGBAModel.Convert(color.Gray{128}))
	fmt.Println(color.NRGBAModel.Convert(color.RGBA{255, 128, 0, 128}))
	fmt.Println(color.AlphaModel.Convert(color.NRGBA{0, 0, 0, 200}))
	fmt.Println(color.CMYKModel.Convert(color.RGBA{255, 128, 0, 255}))
	fmt.Println(color.RGBAModel.Convert(color.CMYK{50, 100, 150, 200}))

	// YCbCr round trips and the conversion helpers.
	for _, rgb := range [][3]uint8{{255, 128, 0}, {0, 0, 0}, {255, 255, 255}, {64, 192, 32}, {10, 20, 30}} {
		y, cb, cr := color.RGBToYCbCr(rgb[0], rgb[1], rgb[2])
		r, g, b := color.YCbCrToRGB(y, cb, cr)
		fmt.Println(y, cb, cr, "->", r, g, b)
		fmt.Println(color.YCbCr{y, cb, cr}.RGBA())
	}

	// CMYK conversion helper and round trip.
	for _, rgb := range [][3]uint8{{255, 128, 0}, {0, 0, 0}, {200, 100, 50}} {
		c, m, yk, k := color.RGBToCMYK(rgb[0], rgb[1], rgb[2])
		r, g, b := color.CMYKToRGB(c, m, yk, k)
		fmt.Println(c, m, yk, k, "->", r, g, b)
	}

	// Black/White/Transparent/Opaque sentinels.
	fmt.Println(color.Black.RGBA())
	fmt.Println(color.White.RGBA())
	fmt.Println(color.Transparent.RGBA())
	fmt.Println(color.Opaque.RGBA())
}
