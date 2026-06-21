// demo_echo: an Echo HTTP service compiled to a .NET assembly by goclr. Like
// demo_gin, the whole program — including the Echo web framework and its
// ACME/autocert TLS dependency closure — is compiled to ECMA-335 IL and runs on
// the CLR with `dotnet`. Echo is a validation target for the compiler, exercising
// a real third-party router/middleware stack (and a larger crypto/x509 surface
// than Gin) on .NET.
package main

import (
	"net/http"

	"github.com/labstack/echo/v4"
)

func main() {
	e := echo.New()
	e.HideBanner = true

	e.GET("/health", func(c echo.Context) error {
		return c.String(http.StatusOK, "ok")
	})

	e.GET("/ping", func(c echo.Context) error {
		return c.JSON(http.StatusOK, map[string]string{"message": "pong"})
	})

	e.GET("/hello/:name", func(c echo.Context) error {
		return c.JSON(http.StatusOK, map[string]string{"hello": c.Param("name")})
	})

	e.Start(":8080")
}
