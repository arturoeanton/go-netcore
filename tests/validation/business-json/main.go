// business-json: a pure business application — order processing with invoicing,
// tax, discounts, and JSON serialization. No goja, no third-party code: just the
// kind of domain logic a SaaS backend runs every day.
package main

import (
	"encoding/json"
	"fmt"
	"sort"
	"strings"
)

type Money int64 // cents

func (m Money) String() string {
	neg := ""
	if m < 0 {
		neg, m = "-", -m
	}
	return fmt.Sprintf("%s$%d.%02d", neg, m/100, m%100)
}

type LineItem struct {
	SKU      string `json:"sku"`
	Name     string `json:"name"`
	Quantity int    `json:"qty"`
	Unit     Money  `json:"unit_price"`
}

func (li LineItem) Subtotal() Money { return li.Unit * Money(li.Quantity) }

type Customer struct {
	ID    string `json:"id"`
	Name  string `json:"name"`
	Tier  string `json:"tier"` // "standard", "gold", "platinum"
	Email string `json:"email,omitempty"`
}

type Order struct {
	ID       string     `json:"id"`
	Customer Customer   `json:"customer"`
	Items    []LineItem `json:"items"`
	TaxRate  float64    `json:"tax_rate"`
}

func (o Order) discountRate() float64 {
	switch o.Customer.Tier {
	case "platinum":
		return 0.15
	case "gold":
		return 0.10
	default:
		return 0
	}
}

type Invoice struct {
	OrderID  string  `json:"order_id"`
	Subtotal Money   `json:"subtotal"`
	Discount Money   `json:"discount"`
	Tax      Money   `json:"tax"`
	Total    Money   `json:"total"`
	Lines    int     `json:"line_count"`
	Rate     float64 `json:"discount_rate"`
}

func (o Order) Invoice() Invoice {
	var sub Money
	for _, li := range o.Items {
		sub += li.Subtotal()
	}
	rate := o.discountRate()
	disc := Money(float64(sub) * rate)
	taxed := sub - disc
	tax := Money(float64(taxed) * o.TaxRate)
	return Invoice{
		OrderID:  o.ID,
		Subtotal: sub,
		Discount: disc,
		Tax:      tax,
		Total:    taxed + tax,
		Lines:    len(o.Items),
		Rate:     rate,
	}
}

func main() {
	raw := `{
		"id": "ORD-1001",
		"customer": {"id":"C-7","name":"Acme Corp","tier":"gold","email":"ap@acme.test"},
		"tax_rate": 0.21,
		"items": [
			{"sku":"WID-1","name":"Widget","qty":10,"unit_price":1299},
			{"sku":"GAD-9","name":"Gadget","qty":3,"unit_price":4999},
			{"sku":"BOLT","name":"Bolt","qty":100,"unit_price":15}
		]
	}`

	var order Order
	if err := json.Unmarshal([]byte(raw), &order); err != nil {
		fmt.Println("parse error:", err)
		return
	}

	fmt.Printf("Order %s for %s (%s tier)\n", order.ID, order.Customer.Name, order.Customer.Tier)

	// sort line items by subtotal descending for the printed summary
	items := make([]LineItem, len(order.Items))
	copy(items, order.Items)
	sort.Slice(items, func(i, j int) bool { return items[i].Subtotal() > items[j].Subtotal() })
	for _, li := range items {
		fmt.Printf("  %-8s x%-4d %10s = %s\n", li.SKU, li.Quantity, li.Unit, li.Subtotal())
	}

	inv := order.Invoice()
	fmt.Println(strings.Repeat("-", 40))
	fmt.Printf("Subtotal: %s\n", inv.Subtotal)
	fmt.Printf("Discount: %s (%.0f%%)\n", inv.Discount, inv.Rate*100)
	fmt.Printf("Tax:      %s\n", inv.Tax)
	fmt.Printf("Total:    %s\n", inv.Total)

	out, _ := json.MarshalIndent(inv, "", "  ")
	fmt.Println(string(out))
}
