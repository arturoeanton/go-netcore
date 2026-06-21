// Validation target: a Gin HTTP router compiled to .NET by goclr. Mirrors the Echo
// target's intent (a real third-party web framework running on the CLR) but Gin is
// lighter — it does not pull in the ACME/autocert TLS subsystem.
package main

import (
	"net/http"

	"github.com/gin-gonic/gin"
)

func main() {
	gin.SetMode(gin.ReleaseMode)
	r := gin.New()

	r.GET("/health", func(c *gin.Context) {
		c.String(http.StatusOK, "ok")
	})

	r.GET("/ping", func(c *gin.Context) {
		c.JSON(http.StatusOK, gin.H{"message": "pong"})
	})

	r.Run(":8080")
}
