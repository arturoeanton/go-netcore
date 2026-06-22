//go:build goclr

package demo_testify

import (
	"errors"
	"testing"

	"github.com/stretchr/testify/assert"
)

type Point struct{ X, Y int }

func TestArithmetic(t *testing.T) {
	assert.Equal(t, 5, Add(2, 3))
	assert.Equal(t, "hi bob", Greet("bob"))
	assert.NotEqual(t, 3, Add(1, 1))
	assert.Greater(t, 5, 3)
}

func TestCollections(t *testing.T) {
	assert.Len(t, []int{1, 2, 3}, 3)
	assert.ElementsMatch(t, []int{1, 2, 3}, []int{3, 1, 2})
	assert.Contains(t, "hello world", "world")
	assert.Contains(t, map[string]int{"a": 1}, "a")
	assert.Equal(t, Point{1, 2}, Point{1, 2})
}

func TestErrors(t *testing.T) {
	assert.NoError(t, nil)
	assert.Error(t, errors.New("boom"))
	assert.Nil(t, nil)
}
