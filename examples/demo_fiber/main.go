// demo_fiber: an integral web app compiled to a .NET assembly by goclr and served
// on the CLR with `dotnet`. The whole stack is pure-Go lowered to ECMA-335 IL:
//
//   - gofiber/fiber/v2 (+ fasthttp router, JSON encoder, HTTP/1.1 parser) — the web layer
//   - dop251/goja — a JavaScript engine, run entirely as managed .NET code
//   - database/sql + go-r2-sqlite — script storage in a real SQLite database
//
// It is a tiny script admin: store a JS snippet under a name, list them, and run
// one on demand — the result computed by the embedded goja VM.
//
//	POST /scripts/:name   body = JS source        -> persists the script
//	GET  /scripts                                 -> JSON list of script names
//	GET  /run/:name                               -> runs the stored script, returns its value
package main

import (
	"database/sql"
	"log"

	_ "github.com/arturoeanton/go-r2-sqlite"
	"github.com/dop251/goja"
	"github.com/gofiber/fiber/v2"
)

func main() {
	db, err := sql.Open("r2sqlite", "/tmp/goclr_scripts.db")
	if err != nil {
		log.Fatal(err)
	}
	if _, err := db.Exec(`CREATE TABLE IF NOT EXISTS scripts (name TEXT PRIMARY KEY, src TEXT)`); err != nil {
		log.Fatal(err)
	}
	// Seed a couple of scripts so GET /run/:name works out of the box.
	seed(db, "sum", `var s = 0; for (var i = 1; i <= 100; i++) s += i; s`)
	seed(db, "upper", `"saas platform".toUpperCase()`)

	app := fiber.New()

	app.Get("/", func(c *fiber.Ctx) error {
		return c.SendString("goclr integral demo: fiber + goja + sqlite\n" +
			"  POST /scripts/:name  (body = JS)\n" +
			"  GET  /scripts\n" +
			"  GET  /run/:name\n")
	})

	// Store a script: the request body is the JavaScript source.
	app.Post("/scripts/:name", func(c *fiber.Ctx) error {
		name := c.Params("name")
		src := string(c.Body())
		if _, err := db.Exec(
			`INSERT OR REPLACE INTO scripts (name, src) VALUES (?, ?)`, name, src); err != nil {
			return c.Status(500).JSON(fiber.Map{"error": err.Error()})
		}
		return c.JSON(fiber.Map{"stored": name})
	})

	// List the stored script names.
	app.Get("/scripts", func(c *fiber.Ctx) error {
		rows, err := db.Query(`SELECT name FROM scripts ORDER BY name`)
		if err != nil {
			return c.Status(500).JSON(fiber.Map{"error": err.Error()})
		}
		defer rows.Close()
		names := []string{}
		for rows.Next() {
			var n string
			if err := rows.Scan(&n); err != nil {
				return c.Status(500).JSON(fiber.Map{"error": err.Error()})
			}
			names = append(names, n)
		}
		return c.JSON(fiber.Map{"scripts": names})
	})

	// Run a stored script through goja and return its value.
	app.Get("/run/:name", func(c *fiber.Ctx) error {
		name := c.Params("name")
		var src string
		if err := db.QueryRow(`SELECT src FROM scripts WHERE name = ?`, name).Scan(&src); err != nil {
			return c.Status(404).JSON(fiber.Map{"error": "no such script: " + name})
		}
		vm := goja.New()
		v, err := vm.RunString(src)
		if err != nil {
			return c.Status(400).JSON(fiber.Map{"error": err.Error()})
		}
		return c.JSON(fiber.Map{"name": name, "result": v.Export()})
	})

	log.Fatal(app.Listen(":3000"))
}

func seed(db *sql.DB, name, src string) {
	db.Exec(`INSERT OR REPLACE INTO scripts (name, src) VALUES (?, ?)`, name, src)
}
