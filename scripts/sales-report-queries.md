# Common Sales Report Queries

These SQLite queries run against [`db-scripts.sqlite.sql`](./db-scripts.sqlite.sql).
They are intended as reference reports for the demo query builder.

## 1. Monthly revenue and invoice volume

```sql
SELECT strftime('%Y-%m', InvoiceDate) AS Month, COUNT(*) AS InvoiceCount,
       SUM(TotalAmount) AS Revenue, AVG(TotalAmount) AS AverageOrderValue
FROM Invoices WHERE Status <> 'Draft' GROUP BY Month ORDER BY Month;
```

## 2. Revenue by customer segment and region

```sql
SELECT c.Segment, c.Region, COUNT(DISTINCT i.Id) AS InvoiceCount,
       SUM(i.TotalAmount) AS Revenue
FROM Customers c JOIN Invoices i ON i.CustomerId = c.Id
WHERE i.Status <> 'Draft' GROUP BY c.Segment, c.Region
ORDER BY Revenue DESC;
```

## 3. Top customers by lifetime value

```sql
SELECT c.Id, c.Name, COUNT(i.Id) AS Orders, SUM(i.TotalAmount) AS LifetimeValue
FROM Customers c JOIN Invoices i ON i.CustomerId = c.Id
WHERE i.Status = 'Paid' GROUP BY c.Id, c.Name
ORDER BY LifetimeValue DESC LIMIT 10;
```

## 4. Best-selling products by units and revenue

```sql
SELECT p.Name, p.Category, SUM(ii.Quantity) AS UnitsSold,
       SUM(ii.LineTotal) AS Revenue
FROM Products p JOIN InvoiceItems ii ON ii.ProductId = p.Id
JOIN Invoices i ON i.Id = ii.InvoiceId WHERE i.Status = 'Paid'
GROUP BY p.Id, p.Name, p.Category ORDER BY Revenue DESC;
```

## 5. Gross margin by product

```sql
SELECT p.Name, SUM(ii.Quantity) AS UnitsSold,
       SUM(ii.LineTotal - (ii.Quantity * p.CostPrice)) AS GrossProfit,
       ROUND(SUM(ii.LineTotal - (ii.Quantity * p.CostPrice))
             * 100.0 / NULLIF(SUM(ii.LineTotal), 0), 2) AS MarginPercent
FROM Products p JOIN InvoiceItems ii ON ii.ProductId = p.Id
JOIN Invoices i ON i.Id = ii.InvoiceId WHERE i.Status = 'Paid'
GROUP BY p.Id, p.Name ORDER BY GrossProfit DESC;
```

## 6. Outstanding receivables and aging

```sql
SELECT i.InvoiceNumber, c.Name, i.InvoiceDate, i.DueDate, i.TotalAmount,
       CAST(julianday('now') - julianday(i.DueDate) AS INTEGER) AS DaysOverdue
FROM Invoices i JOIN Customers c ON c.Id = i.CustomerId
WHERE i.Status = 'Sent' ORDER BY DaysOverdue DESC;
```

## 7. Paid collection time by payment method

```sql
SELECT PaymentMethod, COUNT(*) AS PaidInvoices,
       AVG(julianday(PaidOn) - julianday(InvoiceDate)) AS AverageDaysToPay,
       SUM(TotalAmount) AS Collected
FROM Invoices WHERE Status = 'Paid' GROUP BY PaymentMethod;
```

## 8. Sales-channel performance

```sql
SELECT SalesChannel, COUNT(*) AS Orders, SUM(TotalAmount) AS Revenue,
       AVG(TotalAmount) AS AverageOrderValue
FROM Invoices WHERE Status <> 'Draft' GROUP BY SalesChannel ORDER BY Revenue DESC;
```

## 9. Product attach and cross-sell pairs

```sql
SELECT p1.Name AS Product, p2.Name AS FrequentlyBoughtWith, COUNT(*) AS OrdersTogether
FROM InvoiceItems a 
  JOIN InvoiceItems b ON a.InvoiceId = b.InvoiceId AND a.ProductId < b.ProductId
  JOIN Products p1 ON p1.Id = a.ProductId 
  JOIN Products p2 ON p2.Id = b.ProductId
GROUP BY a.ProductId, b.ProductId ORDER BY OrdersTogether DESC;
```

## 10. Customers with repeat purchases

```sql
SELECT c.Name, COUNT(i.Id) AS OrderCount, MAX(i.InvoiceDate) AS LastOrderDate
FROM Customers c 
  JOIN Invoices i ON i.CustomerId = c.Id
WHERE i.Status = 'Paid' 
GROUP BY c.Id, c.Name 
HAVING COUNT(i.Id) > 1;
```

## 11. Customers with no paid order

```sql
SELECT c.Id, c.Name, c.Segment, c.Region 
FROM Customers c
  LEFT JOIN Invoices i ON i.CustomerId = c.Id AND i.Status = 'Paid'
WHERE i.Id IS NULL 
ORDER BY c.Name;
```

## 12. Low-stock products and inventory value

```sql
SELECT Name, Category, StockQuantity, ReorderLevel,
       StockQuantity * CostPrice AS InventoryCost
FROM Products 
WHERE StockQuantity <= ReorderLevel 
ORDER BY StockQuantity;
```

## 13. Discount impact by month

```sql
SELECT strftime('%Y-%m', InvoiceDate) AS Month, SUM(DiscountAmount) AS Discounts,
       SUM(TotalAmount) AS NetRevenue,
       ROUND(SUM(DiscountAmount) * 100.0 / NULLIF(SUM(TotalAmount), 0), 2) AS DiscountRate
FROM Invoices 
GROUP BY Month 
ORDER BY Month;
```

## 14. Revenue by weekday

```sql
SELECT strftime('%w', InvoiceDate) AS Weekday, COUNT(*) AS Orders,
       SUM(TotalAmount) AS Revenue FROM Invoices WHERE Status = 'Paid'
GROUP BY Weekday ORDER BY Revenue DESC;
```

## 15. Invoice status funnel

```sql
SELECT Status, COUNT(*) AS InvoiceCount, SUM(TotalAmount) AS PipelineValue
FROM Invoices GROUP BY Status ORDER BY PipelineValue DESC;
```

## 16. Average basket composition

```sql
SELECT i.InvoiceNumber, c.Name, COUNT(ii.Id) AS LineCount,
       SUM(ii.Quantity) AS Units, i.TotalAmount
FROM Invoices i JOIN Customers c ON c.Id = i.CustomerId
JOIN InvoiceItems ii ON ii.InvoiceId = i.Id GROUP BY i.Id, i.InvoiceNumber, c.Name, i.TotalAmount
ORDER BY i.TotalAmount DESC;
```

## 17. Category revenue and profitability

```sql
SELECT p.Category, SUM(ii.LineTotal) AS Revenue,
       SUM(ii.LineTotal - ii.Quantity * p.CostPrice) AS GrossProfit
FROM Products p JOIN InvoiceItems ii ON ii.ProductId = p.Id
JOIN Invoices i ON i.Id = ii.InvoiceId WHERE i.Status = 'Paid'
GROUP BY p.Category ORDER BY Revenue DESC;
```

## 18. Monthly revenue trend with prior month

```sql
WITH monthly AS (
  SELECT strftime('%Y-%m', InvoiceDate) AS Month, SUM(TotalAmount) AS Revenue
  FROM Invoices WHERE Status = 'Paid' GROUP BY Month
)
SELECT Month, Revenue, LAG(Revenue) OVER (ORDER BY Month) AS PriorMonthRevenue,
       Revenue - LAG(Revenue) OVER (ORDER BY Month) AS Change
FROM monthly ORDER BY Month;
```

## 19. Customer recency, frequency, and monetary value

```sql
SELECT c.Name, MAX(i.InvoiceDate) AS LastPurchase, COUNT(i.Id) AS Frequency,
       SUM(i.TotalAmount) AS MonetaryValue
FROM Customers c JOIN Invoices i ON i.CustomerId = c.Id AND i.Status = 'Paid'
GROUP BY c.Id, c.Name ORDER BY LastPurchase DESC, MonetaryValue DESC;
```

## 20. Unpaid revenue exposure by customer

```sql
SELECT c.Name, c.Segment, COUNT(i.Id) AS OpenInvoices,
       SUM(i.TotalAmount) AS AmountDue, MIN(i.DueDate) AS OldestDueDate
FROM Customers c JOIN Invoices i ON i.CustomerId = c.Id
WHERE i.Status = 'Sent' GROUP BY c.Id, c.Name, c.Segment
ORDER BY AmountDue DESC;
```
