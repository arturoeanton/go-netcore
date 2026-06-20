// cli-csv: parse CSV from memory, aggregate, and emit a report — a classic CLI/ETL
// batch job. Exercises encoding/csv, maps, sorting, and formatted output.
package main

import (
	"encoding/csv"
	"fmt"
	"sort"
	"strconv"
	"strings"
)

const data = `region,product,units,revenue
EMEA,Widget,120,1560.00
AMER,Widget,200,2600.00
EMEA,Gadget,50,2495.00
APAC,Widget,80,1040.00
AMER,Gadget,30,1497.00
EMEA,Widget,40,520.00
`

type agg struct {
	units   int
	revenue float64
}

func main() {
	r := csv.NewReader(strings.NewReader(data))
	rows, err := r.ReadAll()
	if err != nil {
		fmt.Println("csv error:", err)
		return
	}
	header := rows[0]
	fmt.Println("columns:", strings.Join(header, ","))

	byRegion := map[string]*agg{}
	for _, row := range rows[1:] {
		region := row[0]
		units, _ := strconv.Atoi(row[2])
		rev, _ := strconv.ParseFloat(row[3], 64)
		a := byRegion[region]
		if a == nil {
			a = &agg{}
			byRegion[region] = a
		}
		a.units += units
		a.revenue += rev
	}

	regions := make([]string, 0, len(byRegion))
	for k := range byRegion {
		regions = append(regions, k)
	}
	sort.Strings(regions)

	var totalUnits int
	var totalRev float64
	for _, reg := range regions {
		a := byRegion[reg]
		fmt.Printf("%-6s units=%-5d revenue=%8.2f\n", reg, a.units, a.revenue)
		totalUnits += a.units
		totalRev += a.revenue
	}
	fmt.Printf("TOTAL  units=%-5d revenue=%8.2f\n", totalUnits, totalRev)
}
