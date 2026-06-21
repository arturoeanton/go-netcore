// demo_gin_sql: a Gin REST API backed by database/sql + a pure-Go SQLite driver,
// the whole program compiled to a .NET assembly by goclr and run on the CLR. The Gin
// router, database/sql, AND the SQLite engine (go-r2-sqlite, zero cgo) are all compiled
// to ECMA-335 IL — nothing native, no managed database backend.
package main

import (
	"database/sql"
	"log"
	"net/http"
	"os"
	"path/filepath"
	"strconv"

	"github.com/gin-gonic/gin"

	// Pure-Go, zero-cgo SQLite driver, compiled to IL by goclr alongside the program.
	// Its init() registers the "r2sqlite" driver with database/sql.
	_ "github.com/arturoeanton/go-r2-sqlite"
)

var db *sql.DB

type note struct {
	ID   int64  `json:"id"`
	Text string `json:"text"`
}

func main() {
	var err error
	// A file-backed database is shared across the connection pool (the driver keys
	// open databases by path); start each run from a clean file.
	dbPath := filepath.Join(os.TempDir(), "demo_gin_sql.db")
	os.Remove(dbPath)
	db, err = sql.Open("r2sqlite", dbPath)
	if err != nil {
		log.Fatal(err)
	}
	defer db.Close()
	// Pin the pool to a single connection so every request shares one open database
	// handle (and its page cache).
	db.SetMaxOpenConns(1)

	if _, err := db.Exec(`CREATE TABLE notes (id INTEGER PRIMARY KEY AUTOINCREMENT, text TEXT NOT NULL)`); err != nil {
		log.Fatal(err)
	}
	// Seed a couple of rows.
	for _, t := range []string{"first note", "second note"} {
		if _, err := db.Exec("INSERT INTO notes(text) VALUES(?)", t); err != nil {
			log.Fatal(err)
		}
	}

	gin.SetMode(gin.ReleaseMode)
	r := gin.New()
	r.Use(gin.Recovery())

	// List all notes.
	r.GET("/notes", func(c *gin.Context) {
		rows, err := db.Query("SELECT id, text FROM notes ORDER BY id")
		if err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
			return
		}
		defer rows.Close()
		notes := []note{}
		for rows.Next() {
			var n note
			if err := rows.Scan(&n.ID, &n.Text); err != nil {
				c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
				return
			}
			notes = append(notes, n)
		}
		c.JSON(http.StatusOK, notes)
	})

	// Fetch one note by id.
	r.GET("/notes/:id", func(c *gin.Context) {
		id, _ := strconv.ParseInt(c.Param("id"), 10, 64)
		var n note
		err := db.QueryRow("SELECT id, text FROM notes WHERE id = ?", id).Scan(&n.ID, &n.Text)
		if err == sql.ErrNoRows {
			c.JSON(http.StatusNotFound, gin.H{"error": "not found"})
			return
		}
		if err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
			return
		}
		c.JSON(http.StatusOK, n)
	})

	// Create a note.
	r.POST("/notes", func(c *gin.Context) {
		var in struct {
			Text string `json:"text"`
		}
		// ShouldBindJSON decodes the body into `in`; we validate the field ourselves
		// (gin's struct-tag validator is not yet supported under goclr).
		_ = c.ShouldBindJSON(&in)
		if in.Text == "" {
			c.JSON(http.StatusBadRequest, gin.H{"error": "text required"})
			return
		}
		res, err := db.Exec("INSERT INTO notes(text) VALUES(?)", in.Text)
		if err != nil {
			c.JSON(http.StatusInternalServerError, gin.H{"error": err.Error()})
			return
		}
		id, _ := res.LastInsertId()
		c.JSON(http.StatusCreated, note{ID: id, Text: in.Text})
	})

	r.Run(":8080")
}
