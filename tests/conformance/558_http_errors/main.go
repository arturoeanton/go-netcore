package main

import (
	"errors"
	"fmt"
	"net/http"
)

func main() {
	// MaxBytesError.
	mbe := &http.MaxBytesError{Limit: 1024}
	fmt.Println("maxbytes:", mbe.Error(), "limit:", mbe.Limit)

	// ProtocolError.
	pe := &http.ProtocolError{ErrorString: "malformed chunked encoding"}
	fmt.Println("proto:", pe.Error())

	// ErrNotSupported is a *ProtocolError and Is errors.ErrUnsupported.
	fmt.Println("notsupported:", http.ErrNotSupported.Error())
	fmt.Println("is unsupported:", errors.Is(http.ErrNotSupported, errors.ErrUnsupported))
	fmt.Println("other not unsupported:", errors.Is(pe, errors.ErrUnsupported))
}
