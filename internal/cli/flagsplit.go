package cli

import "strings"

// splitArgs separates flags from positional arguments, allowing flags to appear
// after positionals (e.g. `goclr build ./cmd/server -o ./bin/server.dll`), which
// the standard flag package does not support on its own.
//
// valueFlags names the flags that consume a following token as their value
// (e.g. "o", "profile"); all other flags are treated as booleans. The `-flag=val`
// form is always handled. A bare "--" sends the remainder to positionals.
func splitArgs(args []string, valueFlags map[string]bool) (flags, positionals []string) {
	for i := 0; i < len(args); i++ {
		a := args[i]
		if a == "--" {
			positionals = append(positionals, args[i+1:]...)
			break
		}
		if strings.HasPrefix(a, "-") && a != "-" {
			flags = append(flags, a)
			name := strings.TrimLeft(a, "-")
			if strings.IndexByte(name, '=') >= 0 {
				continue // -flag=value
			}
			if valueFlags[name] && i+1 < len(args) {
				flags = append(flags, args[i+1])
				i++
			}
			continue
		}
		positionals = append(positionals, a)
	}
	return flags, positionals
}
