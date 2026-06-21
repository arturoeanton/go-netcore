// demo_gin: a Gin HTTP service compiled to a .NET assembly by goclr. Like
// demo_goja, the whole program — including the Gin web framework — is compiled to
// ECMA-335 IL and runs on the CLR with `dotnet`. Gin is a validation target for the
// compiler, exercising a real third-party router/middleware stack on .NET.
package main

import (
	"net/http"

	"github.com/gin-gonic/gin"
)

func main() {
	gin.SetMode(gin.ReleaseMode)
	r := gin.New()
	r.Use(gin.Recovery())

	r.GET("/health", func(c *gin.Context) {
		c.String(http.StatusOK, "ok")
	})

	r.GET("/ping", func(c *gin.Context) {
		c.JSON(http.StatusOK, gin.H{"message": "pong"})
	})

	r.GET("/hello/:name", func(c *gin.Context) {
		c.JSON(http.StatusOK, gin.H{"hello": c.Param("name")})
	})

	r.Run(":8080")
}
