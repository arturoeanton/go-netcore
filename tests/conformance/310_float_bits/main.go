package main
import ("fmt";"math";"encoding/binary")
func main(){
	// the goja typed-array reinterpret pattern, done safely
	bits := math.Float64bits(3.14159)
	fmt.Println(bits, math.Float64frombits(bits))
	b := make([]byte, 8)
	binary.LittleEndian.PutUint64(b, bits)
	got := math.Float64frombits(binary.LittleEndian.Uint64(b))
	fmt.Println(got)
	f32 := math.Float32frombits(math.Float32bits(2.5))
	fmt.Println(f32)
}
