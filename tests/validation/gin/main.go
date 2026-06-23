// Validation target: a Gin HTTP router compiled to .NET by goclr, exercised by an
// in-process client (the http-basic shape) so the test runs to completion. Gin wraps
// http.ResponseWriter (embedding the interface, promoting its methods through a
// pointer receiver), so this guards that the response status and body — including a
// framework-generated 404 — reach the client, not just that the program starts.
package main

import (
	"fmt"
	"io"
	"net/http"

	"github.com/gin-gonic/gin"
)

const addr = "127.0.0.1:18080"

func get(path string) (int, string) {
	resp, err := http.Get("http://" + addr + path)
	if err != nil {
		return -1, err.Error()
	}
	body, _ := io.ReadAll(resp.Body)
	resp.Body.Close()
	return resp.StatusCode, string(body)
}

func main() {
	gin.SetMode(gin.ReleaseMode)
	r := gin.New()
	r.GET("/health", func(c *gin.Context) { c.String(http.StatusOK, "ok") })
	r.GET("/ping", func(c *gin.Context) { c.JSON(http.StatusOK, gin.H{"message": "pong"}) })

	go r.Run(addr)

	ready := make(chan struct{})
	go func() {
		for {
			if resp, err := http.Get("http://" + addr + "/health"); err == nil {
				resp.Body.Close()
				close(ready)
				return
			}
		}
	}()
	<-ready

	for _, path := range []string{"/health", "/ping", "/missing"} {
		code, body := get(path)
		fmt.Printf("%s -> %d %s\n", path, code, body)
	}
}
