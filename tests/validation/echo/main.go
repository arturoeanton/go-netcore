// Validation target: an Echo HTTP router compiled to .NET by goclr. Echo is the
// heavier web-framework target — beyond a router/middleware stack it drags in the
// crypto/x509 + acme/autocert TLS subsystem, exercising a much larger stdlib
// surface than Gin. The whole program (framework included) is lowered to ECMA-335
// IL and served on the CLR.
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
