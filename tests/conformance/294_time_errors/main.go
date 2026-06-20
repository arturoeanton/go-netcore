package main
import ("errors";"fmt";"time")
var ErrBase = errors.New("base")
func main(){
	// Duration.String precision >2^53
	fmt.Println(time.Duration(int64(1)<<62).String())
	fmt.Println((90*time.Minute).String(), (1500*time.Millisecond).String(), (500*time.Microsecond).String())
	fmt.Println((3*time.Hour+30*time.Minute+15*time.Second).String())
	fmt.Println((2*time.Hour + 30*time.Minute).Truncate(time.Hour).String())
	fmt.Println((90*time.Second).Round(time.Minute).String())
	// Month/Weekday String
	t := time.Date(2023,time.March,14,0,0,0,0,time.UTC)
	fmt.Println(t.Month().String(), t.Weekday().String())
	// errors %w
	w := fmt.Errorf("wrap: %w", ErrBase)
	fmt.Println(w)
	fmt.Println(errors.Is(w, ErrBase))
	fmt.Println(errors.Unwrap(w) == ErrBase)
	w2 := fmt.Errorf("outer: %w", w)
	fmt.Println(errors.Is(w2, ErrBase))
}
