package main

type MyError struct{ Code int }

func (e MyError) Error() string {
	return "boom"
}

func safeDiv(a, b int) (int, error) {
	if b == 0 {
		return 0, MyError{Code: 1}
	}
	return a / b, nil
}

func main() {
	r, err := safeDiv(10, 2)
	if err != nil {
		println("error:", err.Error())
	} else {
		println("result:", r)
	}
	_, err2 := safeDiv(10, 0)
	if err2 != nil {
		println("error:", err2.Error())
	}
}
