// Validation target: an Echo HTTP router compiled to .NET by goclr, exercised by an
// in-process client so the test runs to completion. Echo is the heavier web-framework
// target — beyond a router/middleware stack it drags in the crypto/x509 + acme/autocert
// TLS subsystem. Echo also wraps http.ResponseWriter, so this guards that response
// status + body — including Echo's JSON 404 — reach the client.
package main

import (
	"fmt"
	"io"
	"net/http"

	"github.com/labstack/echo/v4"
)

const addr = "127.0.0.1:18081"

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
	e := echo.New()
	e.HideBanner = true
	e.HidePort = true
	e.GET("/health", func(c echo.Context) error { return c.String(http.StatusOK, "ok") })
	e.GET("/ping", func(c echo.Context) error { return c.JSON(http.StatusOK, map[string]string{"message": "pong"}) })
	e.GET("/hello/:name", func(c echo.Context) error { return c.JSON(http.StatusOK, map[string]string{"hello": c.Param("name")}) })

	go e.Start(addr)

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

	for _, path := range []string{"/health", "/ping", "/hello/world", "/missing"} {
		code, body := get(path)
		fmt.Printf("%s -> %d %s\n", path, code, body)
	}
}
