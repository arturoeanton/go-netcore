package main

import "fmt"

// fmt's float verbs on a complex value format both parts as "(re±imi)": %f/%F/%e/%E/%g/%G
// (honoring precision) and %#v as Go-syntax "(3.5-2.25i)". The imaginary part always has an
// explicit sign.
func main() {
	c := complex(3.5, -2.25)
	fmt.Printf("%v\n", c)
	fmt.Printf("%f\n", c)
	fmt.Printf("%.2f\n", c)
	fmt.Printf("%e\n", c)
	fmt.Printf("%E\n", c)
	fmt.Printf("%g\n", c)
	fmt.Printf("%G\n", c)
	fmt.Printf("%#v\n", c)
	fmt.Println(c)

	fmt.Printf("%v %v %v\n", complex(0, 1), complex(-1, 0), complex(2, 3))
	fmt.Printf("%f\n", complex(1, 1))
	fmt.Printf("%v\n", complex64(complex(1.5, 2.5)))

	fmt.Printf("%v\n", []complex128{complex(1, 2), complex(3, -4)})
	type T struct{ C complex128 }
	fmt.Printf("%v %+v\n", T{complex(1, 1)}, T{complex(1, 1)})
	fmt.Printf("%v\n", map[string]complex128{"a": complex(1, 2)})
	fmt.Printf("%v\n", c*complex(2, 0))
}
