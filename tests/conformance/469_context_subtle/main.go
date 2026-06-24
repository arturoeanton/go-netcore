package main

import (
	"context"
	"crypto/subtle"
	"errors"
	"fmt"
	"time"
)

func main() {
	fmt.Println(subtle.ConstantTimeLessOrEq(3, 5), subtle.ConstantTimeLessOrEq(5, 3), subtle.ConstantTimeLessOrEq(4, 4))
	x := []byte{1, 2, 3}
	y := []byte{9, 9, 9}
	subtle.ConstantTimeCopy(1, x, y)
	fmt.Println(x)
	x2 := []byte{1, 2, 3}
	subtle.ConstantTimeCopy(0, x2, y)
	fmt.Println(x2)
	subtle.WithDataIndependentTiming(func() { fmt.Println("dit ran") })

	base := context.WithValue(context.Background(), "k", "v")
	c1, cancel := context.WithCancel(base)
	nc := context.WithoutCancel(c1)
	cancel()
	fmt.Println("nc.Err:", nc.Err(), "nc.Value:", nc.Value("k"), "nc.Done==nil:", nc.Done() == nil)

	myCause := errors.New("my cause")
	c2, cf2 := context.WithTimeoutCause(context.Background(), 5*time.Millisecond, myCause)
	<-c2.Done()
	fmt.Println("err:", c2.Err(), "cause:", context.Cause(c2))
	cf2()

	c3, cf3 := context.WithCancel(context.Background())
	done := make(chan bool, 1)
	stop := context.AfterFunc(c3, func() { done <- true })
	cf3()
	<-done
	fmt.Println("afterfunc ran")
	_ = stop

	c4, cf4 := context.WithCancel(context.Background())
	ran := false
	stop2 := context.AfterFunc(c4, func() { ran = true })
	fmt.Println("stopped:", stop2())
	cf4()
	time.Sleep(20 * time.Millisecond)
	fmt.Println("ran after stop:", ran)
}
