// demo_fiber: a gofiber/fiber/v2 web app compiled to a .NET assembly by goclr and
// served on the CLR with `dotnet`. The whole stack — fiber, fasthttp, its router,
// JSON encoder and HTTP/1.1 request parser — is pure-Go lowered to ECMA-335 IL.
package main

import (
	"log"

	"github.com/gofiber/fiber/v2"
)

func main() {
	app := fiber.New()
	app.Get("/", func(c *fiber.Ctx) error {
		return c.SendString("Hello, Fiber on the CLR!")
	})
	app.Get("/api/todos", func(c *fiber.Ctx) error {
		return c.JSON(fiber.Map{"todos": []string{"buy milk", "walk dog"}})
	})
	log.Fatal(app.Listen(":3000"))
}
