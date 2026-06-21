// Command server is the goclr MVP target: an Echo v4 HTTP service that evaluates
// JavaScript via goja. It must eventually compile with `goclr build` and run on
// .NET. See docs/ROADMAP.md for current status.
package main

import (
	"net/http"

	"github.com/dop251/goja"
	"github.com/labstack/echo/v4"
	"github.com/labstack/echo/v4/middleware"
)

type EvalRequest struct {
	Code string `json:"code"`
}

type EvalResponse struct {
	Result any    `json:"result"`
	Error  string `json:"error,omitempty"`
}

func main() {
	e := echo.New()

	e.Use(middleware.Recover())

	e.GET("/health", func(c echo.Context) error {
		return c.String(http.StatusOK, "ok")
	})

	e.POST("/eval", func(c echo.Context) error {
		var req EvalRequest
		if err := c.Bind(&req); err != nil {
			return c.JSON(http.StatusBadRequest, EvalResponse{
				Error: err.Error(),
			})
		}

		vm := goja.New()
		v, err := vm.RunString(req.Code)
		if err != nil {
			return c.JSON(http.StatusBadRequest, EvalResponse{
				Error: err.Error(),
			})
		}

		return c.JSON(http.StatusOK, EvalResponse{
			Result: v.Export(),
		})
	})

	e.Logger.Fatal(e.Start(":8080"))
}
