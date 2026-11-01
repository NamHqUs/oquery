# NorthStar Health Group: dynamic query showcase

NorthStar is a fictional, nationwide health company: it sells health and wellness
products, provides care through clinics, underwrites its own health plans, and
employs the clinicians, sales teams, agents, and support staff behind those
services. `northstar-demo.sqlite.sql` creates the shared SQLite database and
loads its demo data. The provider-specific scripts, including
`northstar-demo.postgres.sql` and `northstar-demo.cockroach.sql`, create the
equivalent schema and seed. The seed is realistic **synthetic data**, not a dump of
actual patient, employee, or customer records. Every report below returns 10
rows against the included seed and uses `LIMIT 10` for a compact demo.

The four domains deliberately share `Users`, `Employees`, `Locations`, and
their relationships. This lets a demo show dynamic projections and aliases,
expression filters, nested joins, aggregates, `HAVING`, subqueries, and window
functions against one realistic scenario rather than four unrelated schemas.

## Research and demo plan

The use cases reflect common operational questions and metrics found in
industry and regulatory guidance:

- **Sales:** revenue and margin by product/channel, customer value, and
  representative performance, following the sales profitability breakdowns in
  [Microsoft's sales and profitability analytics example](https://learn.microsoft.com/en-us/dynamics365/fin-ops-core/dev-itpro/analytics/sales-profitability-performance-content-pack).
- **Health care:** clinic utilization, physician workload, and lab outcomes,
  aligned with practice-operation benchmarks from
  [MGMA](https://www.mgma.com/articles/foundational-benchmarks-and-kpis-for-medical-practice-operations-in-2023).
- **Insurance:** claim status, denial patterns, premiums, paid claims, and a
  simplified medical-loss ratio; real MLR reporting has specific regulatory
  definitions and adjustments, documented in the
  [CMS MLR resources](https://www.cms.gov/marketplace/resources/data/medical-loss-ratio-data-systems-resources).
- **HR:** headcount, compensation, performance, workforce mix, and leave,
  commonly used people-analytics measures described by
  [SHRM](https://www.shrm.org/resourcesandtools/hr-topics/organizational-and-employee-development/pages/hr-metrics-that-matter-most.aspx).

The demo story is: NorthStar sells wellness products to the same people who may
receive care at its clinics and enroll in its insurance plans. Employees connect
the business units as sales representatives, clinicians, insurance agents, and
HR-managed teams. The implementation plan is to (1) establish shared identity,
employee, department, and clinic entities, (2) connect sales orders, clinical
visits/labs, insurance policies/claims/payments, and HR reviews/leaves, (3) seed
enough related records for top-ten grouped reports, and (4) validate all reports
against the SQLite seed and foreign-key constraints.

The data is intentionally invented but plausible and repeatable so the demo is
safe to publish and does not expose protected health or employee information.
The plan comparison in the query pack is a demonstration ratio, not a compliant
regulatory MLR calculation.

## Sales

### 1. Category and channel profitability

Compare sales channels across product categories, including net revenue,
discounts, gross profit, order count, and average basket value.

=== "SQLite"

    ```sql
    WITH order_category AS (
        SELECT o.Id AS OrderId, p.Category, o.Channel,
               SUM(oi.Quantity) AS UnitsSold,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS NetRevenue,
               SUM(oi.DiscountAmount) AS Discounts,
               SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount) AS GrossProfit
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        JOIN Products p ON p.Id = oi.ProductId
        WHERE o.Status = 'Completed'
        GROUP BY o.Id, p.Category, o.Channel
    )
    SELECT Category, Channel,
           COUNT(*) AS OrdersContainingCategory,
           SUM(UnitsSold) AS UnitsSold,
           ROUND(SUM(NetRevenue), 2) AS NetRevenue,
           ROUND(SUM(Discounts), 2) AS Discounts,
           ROUND(SUM(GrossProfit), 2) AS GrossProfit,
           ROUND(AVG(NetRevenue), 2) AS AverageCategoryRevenuePerOrder
    FROM order_category
    GROUP BY Category, Channel
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH order_category AS (
        SELECT o.Id AS OrderId, p.Category, o.Channel,
               SUM(oi.Quantity) AS UnitsSold,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS NetRevenue,
               SUM(oi.DiscountAmount) AS Discounts,
               SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount) AS GrossProfit
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        JOIN Products p ON p.Id = oi.ProductId
        WHERE o.Status = 'Completed'
        GROUP BY o.Id, p.Category, o.Channel
    )
    SELECT TOP (10) Category, Channel,
           COUNT(*) AS OrdersContainingCategory,
           SUM(UnitsSold) AS UnitsSold,
           ROUND(SUM(NetRevenue), 2) AS NetRevenue,
           ROUND(SUM(Discounts), 2) AS Discounts,
           ROUND(SUM(GrossProfit), 2) AS GrossProfit,
           ROUND(AVG(NetRevenue), 2) AS AverageCategoryRevenuePerOrder
    FROM order_category
    GROUP BY Category, Channel
    ORDER BY GrossProfit DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH order_category AS (
        SELECT o.Id AS OrderId, p.Category, o.Channel,
               SUM(oi.Quantity) AS UnitsSold,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS NetRevenue,
               SUM(oi.DiscountAmount) AS Discounts,
               SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount) AS GrossProfit
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        JOIN Products p ON p.Id = oi.ProductId
        WHERE o.Status = 'Completed'
        GROUP BY o.Id, p.Category, o.Channel
    )
    SELECT Category, Channel,
           COUNT(*) AS OrdersContainingCategory,
           SUM(UnitsSold) AS UnitsSold,
           ROUND(SUM(NetRevenue), 2) AS NetRevenue,
           ROUND(SUM(Discounts), 2) AS Discounts,
           ROUND(SUM(GrossProfit), 2) AS GrossProfit,
           ROUND(AVG(NetRevenue), 2) AS AverageCategoryRevenuePerOrder
    FROM order_category
    GROUP BY Category, Channel
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH order_category AS (
        SELECT o.Id AS OrderId, p.Category, o.Channel,
               SUM(oi.Quantity) AS UnitsSold,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS NetRevenue,
               SUM(oi.DiscountAmount) AS Discounts,
               SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount) AS GrossProfit
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        JOIN Products p ON p.Id = oi.ProductId
        WHERE o.Status = 'Completed'
        GROUP BY o.Id, p.Category, o.Channel
    )
    SELECT Category, Channel,
           COUNT(*) AS OrdersContainingCategory,
           SUM(UnitsSold) AS UnitsSold,
           ROUND(SUM(NetRevenue), 2) AS NetRevenue,
           ROUND(SUM(Discounts), 2) AS Discounts,
           ROUND(SUM(GrossProfit), 2) AS GrossProfit,
           ROUND(AVG(NetRevenue), 2) AS AverageCategoryRevenuePerOrder
    FROM order_category
    GROUP BY Category, Channel
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH order_category AS (
        SELECT o.Id AS OrderId, p.Category, o.Channel,
               SUM(oi.Quantity) AS UnitsSold,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS NetRevenue,
               SUM(oi.DiscountAmount) AS Discounts,
               SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount) AS GrossProfit
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        JOIN Products p ON p.Id = oi.ProductId
        WHERE o.Status = 'Completed'
        GROUP BY o.Id, p.Category, o.Channel
    )
    SELECT Category, Channel,
           COUNT(*) AS OrdersContainingCategory,
           SUM(UnitsSold) AS UnitsSold,
           ROUND(SUM(NetRevenue), 2) AS NetRevenue,
           ROUND(SUM(Discounts), 2) AS Discounts,
           ROUND(SUM(GrossProfit), 2) AS GrossProfit,
           ROUND(AVG(NetRevenue), 2) AS AverageCategoryRevenuePerOrder
    FROM order_category
    GROUP BY Category, Channel
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH order_category AS (
        SELECT o.Id AS OrderId, p.Category, o.Channel,
               SUM(oi.Quantity) AS UnitsSold,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS NetRevenue,
               SUM(oi.DiscountAmount) AS Discounts,
               SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount) AS GrossProfit
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        JOIN Products p ON p.Id = oi.ProductId
        WHERE o.Status = 'Completed'
        GROUP BY o.Id, p.Category, o.Channel
    )
    SELECT Category, Channel,
           COUNT(*) AS OrdersContainingCategory,
           SUM(UnitsSold) AS UnitsSold,
           ROUND(SUM(NetRevenue), 2) AS NetRevenue,
           ROUND(SUM(Discounts), 2) AS Discounts,
           ROUND(SUM(GrossProfit), 2) AS GrossProfit,
           ROUND(AVG(NetRevenue), 2) AS AverageCategoryRevenuePerOrder
    FROM order_category
    GROUP BY Category, Channel
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH order_category AS (
        SELECT o.Id AS OrderId, p.Category, o.Channel,
               SUM(oi.Quantity) AS UnitsSold,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS NetRevenue,
               SUM(oi.DiscountAmount) AS Discounts,
               SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount) AS GrossProfit
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        JOIN Products p ON p.Id = oi.ProductId
        WHERE o.Status = 'Completed'
        GROUP BY o.Id, p.Category, o.Channel
    )
    SELECT Category, Channel,
           COUNT(*) AS OrdersContainingCategory,
           SUM(UnitsSold) AS UnitsSold,
           ROUND(SUM(NetRevenue), 2) AS NetRevenue,
           ROUND(SUM(Discounts), 2) AS Discounts,
           ROUND(SUM(GrossProfit), 2) AS GrossProfit,
           ROUND(AVG(NetRevenue), 2) AS AverageCategoryRevenuePerOrder
    FROM order_category
    GROUP BY Category, Channel
    ORDER BY GrossProfit DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH order_category AS (

            SELECT o.Id AS OrderId, p.Category, o.Channel,

                   SUM(oi.Quantity) AS UnitsSold,

                   SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS NetRevenue,

                   SUM(oi.DiscountAmount) AS Discounts,

                   SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount) AS GrossProfit

            FROM SA.Orders o

            JOIN SA.OrderItems oi ON oi.OrderId = o.Id

            JOIN SA.Products p ON p.Id = oi.ProductId

            WHERE o.Status = 'Completed'

            GROUP BY o.Id, p.Category, o.Channel

        )

        SELECT Category, Channel,

               COUNT(*) AS OrdersContainingCategory,

               SUM(UnitsSold) AS UnitsSold,

               ROUND(SUM(NetRevenue), 2) AS NetRevenue,

               ROUND(SUM(Discounts), 2) AS Discounts,

               ROUND(SUM(GrossProfit), 2) AS GrossProfit,

               ROUND(AVG(NetRevenue), 2) AS AverageCategoryRevenuePerOrder

        FROM order_category

        GROUP BY Category, Channel

        ORDER BY GrossProfit DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 2. Customer lifetime value and recency

Rank customers by completed-order value, while showing their purchase
frequency, most recent order, and mix of sales channels.

=== "SQLite"

    ```sql
    SELECT pe.Id AS UserId, pe.FirstName || ' ' || pe.LastName AS Customer,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS LifetimeValue,
           MAX(o.OrderDate) AS LastOrderDate,
           COUNT(DISTINCT o.Channel) AS ChannelsUsed
    FROM Users pe
    JOIN Orders o ON o.UserId = pe.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    WHERE o.Status = 'Completed'
    GROUP BY pe.Id, pe.FirstName, pe.LastName
    HAVING COUNT(DISTINCT o.Id) > 1
    ORDER BY LifetimeValue DESC, LastOrderDate DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) pe.Id AS UserId, CONCAT(pe.FirstName, ' ', pe.LastName) AS Customer,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS LifetimeValue,
           MAX(o.OrderDate) AS LastOrderDate,
           COUNT(DISTINCT o.Channel) AS ChannelsUsed
    FROM Users pe
    JOIN Orders o ON o.UserId = pe.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    WHERE o.Status = 'Completed'
    GROUP BY pe.Id, pe.FirstName, pe.LastName
    HAVING COUNT(DISTINCT o.Id) > 1
    ORDER BY LifetimeValue DESC, LastOrderDate DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT pe.Id AS UserId, pe.FirstName || ' ' || pe.LastName AS Customer,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS LifetimeValue,
           MAX(o.OrderDate) AS LastOrderDate,
           COUNT(DISTINCT o.Channel) AS ChannelsUsed
    FROM Users pe
    JOIN Orders o ON o.UserId = pe.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    WHERE o.Status = 'Completed'
    GROUP BY pe.Id, pe.FirstName, pe.LastName
    HAVING COUNT(DISTINCT o.Id) > 1
    ORDER BY LifetimeValue DESC, LastOrderDate DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT pe.Id AS UserId, pe.FirstName || ' ' || pe.LastName AS Customer,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS LifetimeValue,
           MAX(o.OrderDate) AS LastOrderDate,
           COUNT(DISTINCT o.Channel) AS ChannelsUsed
    FROM Users pe
    JOIN Orders o ON o.UserId = pe.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    WHERE o.Status = 'Completed'
    GROUP BY pe.Id, pe.FirstName, pe.LastName
    HAVING COUNT(DISTINCT o.Id) > 1
    ORDER BY LifetimeValue DESC, LastOrderDate DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT pe.Id AS UserId, CONCAT(pe.FirstName, ' ', pe.LastName) AS Customer,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS LifetimeValue,
           MAX(o.OrderDate) AS LastOrderDate,
           COUNT(DISTINCT o.Channel) AS ChannelsUsed
    FROM Users pe
    JOIN Orders o ON o.UserId = pe.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    WHERE o.Status = 'Completed'
    GROUP BY pe.Id, pe.FirstName, pe.LastName
    HAVING COUNT(DISTINCT o.Id) > 1
    ORDER BY LifetimeValue DESC, LastOrderDate DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT pe.Id AS UserId, pe.FirstName || ' ' || pe.LastName AS Customer,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS LifetimeValue,
           MAX(o.OrderDate) AS LastOrderDate,
           COUNT(DISTINCT o.Channel) AS ChannelsUsed
    FROM Users pe
    JOIN Orders o ON o.UserId = pe.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    WHERE o.Status = 'Completed'
    GROUP BY pe.Id, pe.FirstName, pe.LastName
    HAVING COUNT(DISTINCT o.Id) > 1
    ORDER BY LifetimeValue DESC, LastOrderDate DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT pe.Id AS UserId, pe.FirstName || ' ' || pe.LastName AS Customer,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS LifetimeValue,
           MAX(o.OrderDate) AS LastOrderDate,
           COUNT(DISTINCT o.Channel) AS ChannelsUsed
    FROM Users pe
    JOIN Orders o ON o.UserId = pe.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    WHERE o.Status = 'Completed'
    GROUP BY pe.Id, pe.FirstName, pe.LastName
    HAVING COUNT(DISTINCT o.Id) > 1
    ORDER BY LifetimeValue DESC, LastOrderDate DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT pe.Id AS UserId, pe.FirstName || ' ' || pe.LastName AS Customer,

               COUNT(DISTINCT o.Id) AS OrderCount,

               ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS LifetimeValue,

               MAX(o.OrderDate) AS LastOrderDate,

               COUNT(DISTINCT o.Channel) AS ChannelsUsed

        FROM MAIN.Users pe

        JOIN SA.Orders o ON o.UserId = pe.Id

        JOIN SA.OrderItems oi ON oi.OrderId = o.Id

        WHERE o.Status = 'Completed'

        GROUP BY pe.Id, pe.FirstName, pe.LastName

        HAVING COUNT(DISTINCT o.Id) > 1

        ORDER BY LifetimeValue DESC, LastOrderDate DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 3. Sales representative performance

Show team and location context alongside order volume, net sales, units, and
gross profit. The line-item aggregation is grouped by representative.

=== "SQLite"

    ```sql
    SELECT e.Id AS SalesRepId, e.FirstName || ' ' || e.LastName AS SalesRep,
           l.City AS HomeCity, d.Name AS Department,
           COUNT(DISTINCT o.Id) AS CompletedOrders,
           SUM(oi.Quantity) AS UnitsSold,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetSales,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    JOIN Orders o ON o.SalesRepId = e.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    JOIN Products p ON p.Id = oi.ProductId
    WHERE d.Name = 'Retail Sales' AND o.Status = 'Completed'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, d.Name
    ORDER BY NetSales DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) e.Id AS SalesRepId, CONCAT(e.FirstName, ' ', e.LastName) AS SalesRep,
           l.City AS HomeCity, d.Name AS Department,
           COUNT(DISTINCT o.Id) AS CompletedOrders,
           SUM(oi.Quantity) AS UnitsSold,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetSales,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    JOIN Orders o ON o.SalesRepId = e.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    JOIN Products p ON p.Id = oi.ProductId
    WHERE d.Name = 'Retail Sales' AND o.Status = 'Completed'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, d.Name
    ORDER BY NetSales DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT e.Id AS SalesRepId, e.FirstName || ' ' || e.LastName AS SalesRep,
           l.City AS HomeCity, d.Name AS Department,
           COUNT(DISTINCT o.Id) AS CompletedOrders,
           SUM(oi.Quantity) AS UnitsSold,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetSales,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    JOIN Orders o ON o.SalesRepId = e.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    JOIN Products p ON p.Id = oi.ProductId
    WHERE d.Name = 'Retail Sales' AND o.Status = 'Completed'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, d.Name
    ORDER BY NetSales DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT e.Id AS SalesRepId, e.FirstName || ' ' || e.LastName AS SalesRep,
           l.City AS HomeCity, d.Name AS Department,
           COUNT(DISTINCT o.Id) AS CompletedOrders,
           SUM(oi.Quantity) AS UnitsSold,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetSales,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    JOIN Orders o ON o.SalesRepId = e.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    JOIN Products p ON p.Id = oi.ProductId
    WHERE d.Name = 'Retail Sales' AND o.Status = 'Completed'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, d.Name
    ORDER BY NetSales DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT e.Id AS SalesRepId, CONCAT(e.FirstName, ' ', e.LastName) AS SalesRep,
           l.City AS HomeCity, d.Name AS Department,
           COUNT(DISTINCT o.Id) AS CompletedOrders,
           SUM(oi.Quantity) AS UnitsSold,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetSales,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    JOIN Orders o ON o.SalesRepId = e.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    JOIN Products p ON p.Id = oi.ProductId
    WHERE d.Name = 'Retail Sales' AND o.Status = 'Completed'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, d.Name
    ORDER BY NetSales DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT e.Id AS SalesRepId, e.FirstName || ' ' || e.LastName AS SalesRep,
           l.City AS HomeCity, d.Name AS Department,
           COUNT(DISTINCT o.Id) AS CompletedOrders,
           SUM(oi.Quantity) AS UnitsSold,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetSales,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    JOIN Orders o ON o.SalesRepId = e.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    JOIN Products p ON p.Id = oi.ProductId
    WHERE d.Name = 'Retail Sales' AND o.Status = 'Completed'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, d.Name
    ORDER BY NetSales DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT e.Id AS SalesRepId, e.FirstName || ' ' || e.LastName AS SalesRep,
           l.City AS HomeCity, d.Name AS Department,
           COUNT(DISTINCT o.Id) AS CompletedOrders,
           SUM(oi.Quantity) AS UnitsSold,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetSales,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    JOIN Orders o ON o.SalesRepId = e.Id
    JOIN OrderItems oi ON oi.OrderId = o.Id
    JOIN Products p ON p.Id = oi.ProductId
    WHERE d.Name = 'Retail Sales' AND o.Status = 'Completed'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, d.Name
    ORDER BY NetSales DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT e.Id AS SalesRepId, e.FirstName || ' ' || e.LastName AS SalesRep,

               l.City AS HomeCity, d.Name AS Department,

               COUNT(DISTINCT o.Id) AS CompletedOrders,

               SUM(oi.Quantity) AS UnitsSold,

               ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetSales,

               ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit

        FROM MAIN.Employees e

        JOIN HR.Departments d ON d.Id = e.DepartmentId

        JOIN MAIN.Locations l ON l.Id = e.LocationId

        JOIN SA.Orders o ON o.SalesRepId = e.Id

        JOIN SA.OrderItems oi ON oi.OrderId = o.Id

        JOIN SA.Products p ON p.Id = oi.ProductId

        WHERE d.Name = 'Retail Sales' AND o.Status = 'Completed'

        GROUP BY e.Id, e.FirstName, e.LastName, l.City, d.Name

        ORDER BY NetSales DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 4. Best-selling products and margin

Compare units, orders, realized revenue, discounted gross profit, and margin
percentage by product.

=== "SQLite"

    ```sql
    SELECT p.Id AS ProductId, p.Name AS Product, p.Category,
           SUM(oi.Quantity) AS UnitsSold,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetRevenue,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit,
           ROUND(100.0 * SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount)
                 / NULLIF(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 0), 2) AS MarginPercent
    FROM Products p
    JOIN OrderItems oi ON oi.ProductId = p.Id
    JOIN Orders o ON o.Id = oi.OrderId
    WHERE o.Status = 'Completed'
    GROUP BY p.Id, p.Name, p.Category
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) p.Id AS ProductId, p.Name AS Product, p.Category,
           SUM(oi.Quantity) AS UnitsSold,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetRevenue,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit,
           ROUND(100.0 * SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount)
                 / NULLIF(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 0), 2) AS MarginPercent
    FROM Products p
    JOIN OrderItems oi ON oi.ProductId = p.Id
    JOIN Orders o ON o.Id = oi.OrderId
    WHERE o.Status = 'Completed'
    GROUP BY p.Id, p.Name, p.Category
    ORDER BY GrossProfit DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT p.Id AS ProductId, p.Name AS Product, p.Category,
           SUM(oi.Quantity) AS UnitsSold,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetRevenue,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit,
           ROUND(100.0 * SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount)
                 / NULLIF(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 0), 2) AS MarginPercent
    FROM Products p
    JOIN OrderItems oi ON oi.ProductId = p.Id
    JOIN Orders o ON o.Id = oi.OrderId
    WHERE o.Status = 'Completed'
    GROUP BY p.Id, p.Name, p.Category
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT p.Id AS ProductId, p.Name AS Product, p.Category,
           SUM(oi.Quantity) AS UnitsSold,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetRevenue,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit,
           ROUND(100.0 * SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount)
                 / NULLIF(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 0), 2) AS MarginPercent
    FROM Products p
    JOIN OrderItems oi ON oi.ProductId = p.Id
    JOIN Orders o ON o.Id = oi.OrderId
    WHERE o.Status = 'Completed'
    GROUP BY p.Id, p.Name, p.Category
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT p.Id AS ProductId, p.Name AS Product, p.Category,
           SUM(oi.Quantity) AS UnitsSold,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetRevenue,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit,
           ROUND(100.0 * SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount)
                 / NULLIF(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 0), 2) AS MarginPercent
    FROM Products p
    JOIN OrderItems oi ON oi.ProductId = p.Id
    JOIN Orders o ON o.Id = oi.OrderId
    WHERE o.Status = 'Completed'
    GROUP BY p.Id, p.Name, p.Category
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT p.Id AS ProductId, p.Name AS Product, p.Category,
           SUM(oi.Quantity) AS UnitsSold,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetRevenue,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit,
           ROUND(100.0 * SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount)
                 / NULLIF(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 0), 2) AS MarginPercent
    FROM Products p
    JOIN OrderItems oi ON oi.ProductId = p.Id
    JOIN Orders o ON o.Id = oi.OrderId
    WHERE o.Status = 'Completed'
    GROUP BY p.Id, p.Name, p.Category
    ORDER BY GrossProfit DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT p.Id AS ProductId, p.Name AS Product, p.Category,
           SUM(oi.Quantity) AS UnitsSold,
           COUNT(DISTINCT o.Id) AS OrderCount,
           ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetRevenue,
           ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit,
           ROUND(100.0 * SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount)
                 / NULLIF(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 0), 2) AS MarginPercent
    FROM Products p
    JOIN OrderItems oi ON oi.ProductId = p.Id
    JOIN Orders o ON o.Id = oi.OrderId
    WHERE o.Status = 'Completed'
    GROUP BY p.Id, p.Name, p.Category
    ORDER BY GrossProfit DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT p.Id AS ProductId, p.Name AS Product, p.Category,

               SUM(oi.Quantity) AS UnitsSold,

               COUNT(DISTINCT o.Id) AS OrderCount,

               ROUND(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 2) AS NetRevenue,

               ROUND(SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount), 2) AS GrossProfit,

               ROUND(100.0 * SUM(oi.Quantity * (oi.UnitPrice - p.CostPrice) - oi.DiscountAmount)

                     / NULLIF(SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount), 0), 2) AS MarginPercent

        FROM SA.Products p

        JOIN SA.OrderItems oi ON oi.ProductId = p.Id

        JOIN SA.Orders o ON o.Id = oi.OrderId

        WHERE o.Status = 'Completed'

        GROUP BY p.Id, p.Name, p.Category

        ORDER BY GrossProfit DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 5. Monthly sales trend and momentum

Aggregate completed sales by month, then use window functions for prior-month
comparison and year-to-date running revenue.

=== "SQLite"

    ```sql
    WITH monthly AS (
        SELECT strftime('%Y-%m', o.OrderDate) AS SalesMonth,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS Revenue,
               COUNT(DISTINCT o.Id) AS OrderCount
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        WHERE o.Status = 'Completed'
        GROUP BY strftime('%Y-%m', o.OrderDate)
    )
    SELECT SalesMonth, ROUND(Revenue, 2) AS Revenue, OrderCount,
           ROUND(LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS PriorMonthRevenue,
           ROUND(Revenue - LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS RevenueChange,
           ROUND(SUM(Revenue) OVER (ORDER BY SalesMonth), 2) AS RunningRevenue
    FROM monthly
    ORDER BY SalesMonth DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH monthly AS (
        SELECT CONVERT(char(7), o.OrderDate, 126) AS SalesMonth,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS Revenue,
               COUNT(DISTINCT o.Id) AS OrderCount
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        WHERE o.Status = 'Completed'
        GROUP BY CONVERT(char(7), o.OrderDate, 126)
    )
    SELECT TOP (10) SalesMonth, ROUND(Revenue, 2) AS Revenue, OrderCount,
           ROUND(LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS PriorMonthRevenue,
           ROUND(Revenue - LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS RevenueChange,
           ROUND(SUM(Revenue) OVER (ORDER BY SalesMonth), 2) AS RunningRevenue
    FROM monthly
    ORDER BY SalesMonth DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH monthly AS (
        SELECT TO_CHAR(o.OrderDate, 'YYYY-MM') AS SalesMonth,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS Revenue,
               COUNT(DISTINCT o.Id) AS OrderCount
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        WHERE o.Status = 'Completed'
        GROUP BY TO_CHAR(o.OrderDate, 'YYYY-MM')
    )
    SELECT SalesMonth, ROUND(Revenue, 2) AS Revenue, OrderCount,
           ROUND(LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS PriorMonthRevenue,
           ROUND(Revenue - LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS RevenueChange,
           ROUND(SUM(Revenue) OVER (ORDER BY SalesMonth), 2) AS RunningRevenue
    FROM monthly
    ORDER BY SalesMonth DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH monthly AS (
        SELECT TO_CHAR(o.OrderDate, 'YYYY-MM') AS SalesMonth,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS Revenue,
               COUNT(DISTINCT o.Id) AS OrderCount
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        WHERE o.Status = 'Completed'
        GROUP BY TO_CHAR(o.OrderDate, 'YYYY-MM')
    )
    SELECT SalesMonth, ROUND(Revenue, 2) AS Revenue, OrderCount,
           ROUND(LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS PriorMonthRevenue,
           ROUND(Revenue - LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS RevenueChange,
           ROUND(SUM(Revenue) OVER (ORDER BY SalesMonth), 2) AS RunningRevenue
    FROM monthly
    ORDER BY SalesMonth DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH monthly AS (
        SELECT DATE_FORMAT(o.OrderDate, '%Y-%m') AS SalesMonth,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS Revenue,
               COUNT(DISTINCT o.Id) AS OrderCount
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        WHERE o.Status = 'Completed'
        GROUP BY DATE_FORMAT(o.OrderDate, '%Y-%m')
    )
    SELECT SalesMonth, ROUND(Revenue, 2) AS Revenue, OrderCount,
           ROUND(LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS PriorMonthRevenue,
           ROUND(Revenue - LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS RevenueChange,
           ROUND(SUM(Revenue) OVER (ORDER BY SalesMonth), 2) AS RunningRevenue
    FROM monthly
    ORDER BY SalesMonth DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH monthly AS (
        SELECT TO_VARCHAR(o.OrderDate, 'YYYY-MM') AS SalesMonth,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS Revenue,
               COUNT(DISTINCT o.Id) AS OrderCount
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        WHERE o.Status = 'Completed'
        GROUP BY TO_VARCHAR(o.OrderDate, 'YYYY-MM')
    )
    SELECT SalesMonth, ROUND(Revenue, 2) AS Revenue, OrderCount,
           ROUND(LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS PriorMonthRevenue,
           ROUND(Revenue - LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS RevenueChange,
           ROUND(SUM(Revenue) OVER (ORDER BY SalesMonth), 2) AS RunningRevenue
    FROM monthly
    ORDER BY SalesMonth DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH monthly AS (
        SELECT SUBSTRING(CAST(o.OrderDate AS VARCHAR(10)) FROM 1 FOR 7) AS SalesMonth,
               SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS Revenue,
               COUNT(DISTINCT o.Id) AS OrderCount
        FROM Orders o
        JOIN OrderItems oi ON oi.OrderId = o.Id
        WHERE o.Status = 'Completed'
        GROUP BY SUBSTRING(CAST(o.OrderDate AS VARCHAR(10)) FROM 1 FOR 7)
    )
    SELECT SalesMonth, ROUND(Revenue, 2) AS Revenue, OrderCount,
           ROUND(LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS PriorMonthRevenue,
           ROUND(Revenue - LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS RevenueChange,
           ROUND(SUM(Revenue) OVER (ORDER BY SalesMonth), 2) AS RunningRevenue
    FROM monthly
    ORDER BY SalesMonth DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH monthly AS (

            SELECT VARCHAR_FORMAT(o.OrderDate, 'YYYY-MM') AS SalesMonth,

                   SUM(oi.Quantity * oi.UnitPrice - oi.DiscountAmount) AS Revenue,

                   COUNT(DISTINCT o.Id) AS OrderCount

            FROM SA.Orders o

            JOIN SA.OrderItems oi ON oi.OrderId = o.Id

            WHERE o.Status = 'Completed'

            GROUP BY VARCHAR_FORMAT(o.OrderDate, 'YYYY-MM')

        )

        SELECT SalesMonth, ROUND(Revenue, 2) AS Revenue, OrderCount,

               ROUND(LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS PriorMonthRevenue,

               ROUND(Revenue - LAG(Revenue) OVER (ORDER BY SalesMonth), 2) AS RevenueChange,

               ROUND(SUM(Revenue) OVER (ORDER BY SalesMonth), 2) AS RunningRevenue

        FROM monthly

        ORDER BY SalesMonth DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

## Health care

### 1. Diagnosis burden by clinic

Compare completed visit volume, unique patients, charges, and abnormal
laboratory results by diagnosis and clinic.

=== "SQLite"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(IsAbnormal) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT d.Name AS Diagnosis, d.Category AS DiagnosisCategory,
           l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS UniquePatients,
           ROUND(AVG(v.Charge), 2) AS AverageCharge,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Visits v
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    JOIN Locations l ON l.Id = v.LocationId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE v.Status = 'Completed'
    GROUP BY d.Id, d.Name, d.Category, l.Id, l.City
    ORDER BY VisitCount DESC, AbnormalLabPercent DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CAST(IsAbnormal AS int)) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT TOP (10) d.Name AS Diagnosis, d.Category AS DiagnosisCategory,
           l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS UniquePatients,
           ROUND(AVG(v.Charge), 2) AS AverageCharge,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Visits v
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    JOIN Locations l ON l.Id = v.LocationId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE v.Status = 'Completed'
    GROUP BY d.Id, d.Name, d.Category, l.Id, l.City
    ORDER BY VisitCount DESC, AbnormalLabPercent DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT d.Name AS Diagnosis, d.Category AS DiagnosisCategory,
           l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS UniquePatients,
           ROUND(AVG(v.Charge), 2) AS AverageCharge,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Visits v
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    JOIN Locations l ON l.Id = v.LocationId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE v.Status = 'Completed'
    GROUP BY d.Id, d.Name, d.Category, l.Id, l.City
    ORDER BY VisitCount DESC, AbnormalLabPercent DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT d.Name AS Diagnosis, d.Category AS DiagnosisCategory,
           l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS UniquePatients,
           ROUND(AVG(v.Charge), 2) AS AverageCharge,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Visits v
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    JOIN Locations l ON l.Id = v.LocationId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE v.Status = 'Completed'
    GROUP BY d.Id, d.Name, d.Category, l.Id, l.City
    ORDER BY VisitCount DESC, AbnormalLabPercent DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT d.Name AS Diagnosis, d.Category AS DiagnosisCategory,
           l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS UniquePatients,
           ROUND(AVG(v.Charge), 2) AS AverageCharge,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Visits v
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    JOIN Locations l ON l.Id = v.LocationId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE v.Status = 'Completed'
    GROUP BY d.Id, d.Name, d.Category, l.Id, l.City
    ORDER BY VisitCount DESC, AbnormalLabPercent DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal = 1 THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT d.Name AS Diagnosis, d.Category AS DiagnosisCategory,
           l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS UniquePatients,
           ROUND(AVG(v.Charge), 2) AS AverageCharge,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Visits v
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    JOIN Locations l ON l.Id = v.LocationId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE v.Status = 'Completed'
    GROUP BY d.Id, d.Name, d.Category, l.Id, l.City
    ORDER BY VisitCount DESC, AbnormalLabPercent DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT d.Name AS Diagnosis, d.Category AS DiagnosisCategory,
           l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS UniquePatients,
           ROUND(AVG(v.Charge), 2) AS AverageCharge,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Visits v
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    JOIN Locations l ON l.Id = v.LocationId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE v.Status = 'Completed'
    GROUP BY d.Id, d.Name, d.Category, l.Id, l.City
    ORDER BY VisitCount DESC, AbnormalLabPercent DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH visit_labs AS (

            SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount

            FROM HE.LabResults

            GROUP BY VisitId

        )

        SELECT d.Name AS Diagnosis, d.Category AS DiagnosisCategory,

               l.City AS ClinicCity,

               COUNT(DISTINCT v.Id) AS VisitCount,

               COUNT(DISTINCT v.UserId) AS UniquePatients,

               ROUND(AVG(v.Charge), 2) AS AverageCharge,

               ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))

                     / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent

        FROM HE.Visits v

        JOIN HE.Diagnoses d ON d.Id = v.DiagnosisId

        JOIN MAIN.Locations l ON l.Id = v.LocationId

        LEFT JOIN visit_labs vl ON vl.VisitId = v.Id

        WHERE v.Status = 'Completed'

        GROUP BY d.Id, d.Name, d.Category, l.Id, l.City

        ORDER BY VisitCount DESC, AbnormalLabPercent DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 2. Physician workload and care-quality scorecard

Combine each physician's workload, patient panel, charges, visit mix, and
abnormal lab percentage without losing doctors who have no sampled visits.

=== "SQLite"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(IsAbnormal) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT e.Id AS DoctorId, e.FirstName || ' ' || e.LastName AS Physician,
           d.Name AS Department, l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS PatientCount,
           ROUND(AVG(v.DurationMinutes), 1) AS AverageVisitMinutes,
           ROUND(SUM(v.Charge), 2) AS Charges,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Visits v ON v.DoctorId = e.Id AND v.Status = 'Completed'
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE d.Name IN ('Primary Care', 'Specialty Care', 'Emergency Medicine')
    GROUP BY e.Id, e.FirstName, e.LastName, d.Name, l.City
    ORDER BY VisitCount DESC, Charges DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CAST(IsAbnormal AS int)) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT TOP (10) e.Id AS DoctorId, CONCAT(e.FirstName, ' ', e.LastName) AS Physician,
           d.Name AS Department, l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS PatientCount,
           ROUND(AVG(v.DurationMinutes), 1) AS AverageVisitMinutes,
           ROUND(SUM(v.Charge), 2) AS Charges,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Visits v ON v.DoctorId = e.Id AND v.Status = 'Completed'
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE d.Name IN ('Primary Care', 'Specialty Care', 'Emergency Medicine')
    GROUP BY e.Id, e.FirstName, e.LastName, d.Name, l.City
    ORDER BY VisitCount DESC, Charges DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT e.Id AS DoctorId, e.FirstName || ' ' || e.LastName AS Physician,
           d.Name AS Department, l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS PatientCount,
           ROUND(AVG(v.DurationMinutes), 1) AS AverageVisitMinutes,
           ROUND(SUM(v.Charge), 2) AS Charges,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Visits v ON v.DoctorId = e.Id AND v.Status = 'Completed'
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE d.Name IN ('Primary Care', 'Specialty Care', 'Emergency Medicine')
    GROUP BY e.Id, e.FirstName, e.LastName, d.Name, l.City
    ORDER BY VisitCount DESC, Charges DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT e.Id AS DoctorId, e.FirstName || ' ' || e.LastName AS Physician,
           d.Name AS Department, l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS PatientCount,
           ROUND(AVG(v.DurationMinutes), 1) AS AverageVisitMinutes,
           ROUND(SUM(v.Charge), 2) AS Charges,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Visits v ON v.DoctorId = e.Id AND v.Status = 'Completed'
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE d.Name IN ('Primary Care', 'Specialty Care', 'Emergency Medicine')
    GROUP BY e.Id, e.FirstName, e.LastName, d.Name, l.City
    ORDER BY VisitCount DESC, Charges DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT e.Id AS DoctorId, CONCAT(e.FirstName, ' ', e.LastName) AS Physician,
           d.Name AS Department, l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS PatientCount,
           ROUND(AVG(v.DurationMinutes), 1) AS AverageVisitMinutes,
           ROUND(SUM(v.Charge), 2) AS Charges,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Visits v ON v.DoctorId = e.Id AND v.Status = 'Completed'
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE d.Name IN ('Primary Care', 'Specialty Care', 'Emergency Medicine')
    GROUP BY e.Id, e.FirstName, e.LastName, d.Name, l.City
    ORDER BY VisitCount DESC, Charges DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal = 1 THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT e.Id AS DoctorId, e.FirstName || ' ' || e.LastName AS Physician,
           d.Name AS Department, l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS PatientCount,
           ROUND(AVG(v.DurationMinutes), 1) AS AverageVisitMinutes,
           ROUND(SUM(v.Charge), 2) AS Charges,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Visits v ON v.DoctorId = e.Id AND v.Status = 'Completed'
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE d.Name IN ('Primary Care', 'Specialty Care', 'Emergency Medicine')
    GROUP BY e.Id, e.FirstName, e.LastName, d.Name, l.City
    ORDER BY VisitCount DESC, Charges DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT e.Id AS DoctorId, e.FirstName || ' ' || e.LastName AS Physician,
           d.Name AS Department, l.City AS ClinicCity,
           COUNT(DISTINCT v.Id) AS VisitCount,
           COUNT(DISTINCT v.UserId) AS PatientCount,
           ROUND(AVG(v.DurationMinutes), 1) AS AverageVisitMinutes,
           ROUND(SUM(v.Charge), 2) AS Charges,
           ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))
                 / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Visits v ON v.DoctorId = e.Id AND v.Status = 'Completed'
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    WHERE d.Name IN ('Primary Care', 'Specialty Care', 'Emergency Medicine')
    GROUP BY e.Id, e.FirstName, e.LastName, d.Name, l.City
    ORDER BY VisitCount DESC, Charges DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH visit_labs AS (

            SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount

            FROM HE.LabResults

            GROUP BY VisitId

        )

        SELECT e.Id AS DoctorId, e.FirstName || ' ' || e.LastName AS Physician,

               d.Name AS Department, l.City AS ClinicCity,

               COUNT(DISTINCT v.Id) AS VisitCount,

               COUNT(DISTINCT v.UserId) AS PatientCount,

               ROUND(AVG(v.DurationMinutes), 1) AS AverageVisitMinutes,

               ROUND(SUM(v.Charge), 2) AS Charges,

               ROUND(100.0 * SUM(COALESCE(vl.AbnormalCount, 0))

                     / NULLIF(SUM(COALESCE(vl.LabCount, 0)), 0), 2) AS AbnormalLabPercent

        FROM MAIN.Employees e

        JOIN HR.Departments d ON d.Id = e.DepartmentId

        JOIN MAIN.Locations l ON l.Id = e.LocationId

        LEFT JOIN HE.Visits v ON v.DoctorId = e.Id AND v.Status = 'Completed'

        LEFT JOIN visit_labs vl ON vl.VisitId = v.Id

        WHERE d.Name IN ('Primary Care', 'Specialty Care', 'Emergency Medicine')

        GROUP BY e.Id, e.FirstName, e.LastName, d.Name, l.City

        ORDER BY VisitCount DESC, Charges DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 3. High-risk patient care profile

Surface patients with chronic diagnoses and abnormal results, including
utilization, total charges, and most recent care date.

=== "SQLite"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(IsAbnormal) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT pe.Id AS PatientId, pe.FirstName || ' ' || pe.LastName AS Patient,
           CAST((julianday('2025-10-31') - julianday(pe.BirthDate)) / 365.25 AS INTEGER) AS Age,
           COUNT(DISTINCT CASE WHEN d.IsChronic = 1 THEN v.Id END) AS ChronicVisits,
           COUNT(DISTINCT v.Id) AS CompletedVisits,
           SUM(COALESCE(vl.AbnormalCount, 0)) AS AbnormalResults,
           ROUND(SUM(v.Charge), 2) AS TotalCharges,
           MAX(v.VisitDate) AS LastVisitDate
    FROM Users pe
    JOIN Visits v ON v.UserId = pe.Id AND v.Status = 'Completed'
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    GROUP BY pe.Id, pe.FirstName, pe.LastName, pe.BirthDate
    HAVING ChronicVisits > 0 AND AbnormalResults > 0
    ORDER BY AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CAST(IsAbnormal AS int)) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT TOP (10) pe.Id AS PatientId, CONCAT(pe.FirstName, ' ', pe.LastName) AS Patient,
           CAST(DATEDIFF(DAY, pe.BirthDate, CONVERT(date, '2025-10-31')) / 365.25 AS int) AS Age,
           COUNT(DISTINCT CASE WHEN d.IsChronic = 1 THEN v.Id END) AS ChronicVisits,
           COUNT(DISTINCT v.Id) AS CompletedVisits,
           SUM(COALESCE(vl.AbnormalCount, 0)) AS AbnormalResults,
           ROUND(SUM(v.Charge), 2) AS TotalCharges,
           MAX(v.VisitDate) AS LastVisitDate
    FROM Users pe
    JOIN Visits v ON v.UserId = pe.Id AND v.Status = 'Completed'
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    GROUP BY pe.Id, pe.FirstName, pe.LastName, pe.BirthDate
    HAVING COUNT(DISTINCT CASE WHEN d.IsChronic = 1 THEN v.Id END) > 0
       AND SUM(COALESCE(vl.AbnormalCount, 0)) > 0
    ORDER BY AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT pe.Id AS PatientId, pe.FirstName || ' ' || pe.LastName AS Patient,
           CAST((DATE '2025-10-31' - pe.BirthDate) / 365.25 AS integer) AS Age,
           COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) AS ChronicVisits,
           COUNT(DISTINCT v.Id) AS CompletedVisits,
           SUM(COALESCE(vl.AbnormalCount, 0)) AS AbnormalResults,
           ROUND(SUM(v.Charge), 2) AS TotalCharges,
           MAX(v.VisitDate) AS LastVisitDate
    FROM Users pe
    JOIN Visits v ON v.UserId = pe.Id AND v.Status = 'Completed'
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    GROUP BY pe.Id, pe.FirstName, pe.LastName, pe.BirthDate
    HAVING COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) > 0
       AND SUM(COALESCE(vl.AbnormalCount, 0)) > 0
    ORDER BY AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT pe.Id AS PatientId, pe.FirstName || ' ' || pe.LastName AS Patient,
           (EXTRACT(YEAR FROM DATE '2025-10-31') - EXTRACT(YEAR FROM pe.BirthDate) - CASE WHEN EXTRACT(MONTH FROM DATE '2025-10-31') * 100 + EXTRACT(DAY FROM DATE '2025-10-31') < EXTRACT(MONTH FROM pe.BirthDate) * 100 + EXTRACT(DAY FROM pe.BirthDate) THEN 1 ELSE 0 END) AS Age,
           COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) AS ChronicVisits,
           COUNT(DISTINCT v.Id) AS CompletedVisits,
           SUM(COALESCE(vl.AbnormalCount, 0)) AS AbnormalResults,
           ROUND(SUM(v.Charge), 2) AS TotalCharges,
           MAX(v.VisitDate) AS LastVisitDate
    FROM Users pe
    JOIN Visits v ON v.UserId = pe.Id AND v.Status = 'Completed'
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    GROUP BY pe.Id, pe.FirstName, pe.LastName, pe.BirthDate
    HAVING COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) > 0
       AND SUM(COALESCE(vl.AbnormalCount, 0)) > 0
    ORDER BY AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT pe.Id AS PatientId, CONCAT(pe.FirstName, ' ', pe.LastName) AS Patient,
           TIMESTAMPDIFF(YEAR, pe.BirthDate, '2025-10-31') AS Age,
           COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) AS ChronicVisits,
           COUNT(DISTINCT v.Id) AS CompletedVisits,
           SUM(COALESCE(vl.AbnormalCount, 0)) AS AbnormalResults,
           ROUND(SUM(v.Charge), 2) AS TotalCharges,
           MAX(v.VisitDate) AS LastVisitDate
    FROM Users pe
    JOIN Visits v ON v.UserId = pe.Id AND v.Status = 'Completed'
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    GROUP BY pe.Id, pe.FirstName, pe.LastName, pe.BirthDate
    HAVING COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) > 0
       AND SUM(COALESCE(vl.AbnormalCount, 0)) > 0
    ORDER BY AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal = 1 THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT pe.Id AS PatientId, pe.FirstName || ' ' || pe.LastName AS Patient,
           YEARS_BETWEEN(pe.BirthDate, '2025-10-31') AS Age,
           COUNT(DISTINCT CASE WHEN d.IsChronic = 1 THEN v.Id END) AS ChronicVisits,
           COUNT(DISTINCT v.Id) AS CompletedVisits,
           SUM(COALESCE(vl.AbnormalCount, 0)) AS AbnormalResults,
           ROUND(SUM(v.Charge), 2) AS TotalCharges,
           MAX(v.VisitDate) AS LastVisitDate
    FROM Users pe
    JOIN Visits v ON v.UserId = pe.Id AND v.Status = 'Completed'
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    GROUP BY pe.Id, pe.FirstName, pe.LastName, pe.BirthDate
    HAVING COUNT(DISTINCT CASE WHEN d.IsChronic = 1 THEN v.Id END) > 0
       AND SUM(COALESCE(vl.AbnormalCount, 0)) > 0
    ORDER BY AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH visit_labs AS (
        SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount
        FROM LabResults
        GROUP BY VisitId
    )
    SELECT pe.Id AS PatientId, pe.FirstName || ' ' || pe.LastName AS Patient,
           CAST((DATE '2025-10-31' - pe.BirthDate) / 365.25 AS integer) AS Age,
           COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) AS ChronicVisits,
           COUNT(DISTINCT v.Id) AS CompletedVisits,
           SUM(COALESCE(vl.AbnormalCount, 0)) AS AbnormalResults,
           ROUND(SUM(v.Charge), 2) AS TotalCharges,
           MAX(v.VisitDate) AS LastVisitDate
    FROM Users pe
    JOIN Visits v ON v.UserId = pe.Id AND v.Status = 'Completed'
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    LEFT JOIN visit_labs vl ON vl.VisitId = v.Id
    GROUP BY pe.Id, pe.FirstName, pe.LastName, pe.BirthDate
    HAVING COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) > 0
       AND SUM(COALESCE(vl.AbnormalCount, 0)) > 0
    ORDER BY AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH visit_labs AS (

            SELECT VisitId, COUNT(*) AS LabCount, SUM(CASE WHEN IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount

            FROM HE.LabResults

            GROUP BY VisitId

        )

        SELECT pe.Id AS PatientId, pe.FirstName || ' ' || pe.LastName AS Patient,

               (YEAR(DATE '2025-10-31') - YEAR(pe.BirthDate) - CASE WHEN MONTH(DATE '2025-10-31') * 100 + DAY(DATE '2025-10-31') < MONTH(pe.BirthDate) * 100 + DAY(pe.BirthDate) THEN 1 ELSE 0 END) AS Age,

               COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) AS ChronicVisits,

               COUNT(DISTINCT v.Id) AS CompletedVisits,

               SUM(COALESCE(vl.AbnormalCount, 0)) AS AbnormalResults,

               ROUND(SUM(v.Charge), 2) AS TotalCharges,

               MAX(v.VisitDate) AS LastVisitDate

        FROM MAIN.Users pe

        JOIN HE.Visits v ON v.UserId = pe.Id AND v.Status = 'Completed'

        JOIN HE.Diagnoses d ON d.Id = v.DiagnosisId

        LEFT JOIN visit_labs vl ON vl.VisitId = v.Id

        GROUP BY pe.Id, pe.FirstName, pe.LastName, pe.BirthDate

        HAVING COUNT(DISTINCT CASE WHEN d.IsChronic THEN v.Id END) > 0

           AND SUM(COALESCE(vl.AbnormalCount, 0)) > 0

        ORDER BY AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 4. Lab abnormality patterns

Find test and diagnosis-category combinations with the highest abnormal result
rates, including average observed value and sample size.

=== "SQLite"

    ```sql
    SELECT lr.TestName, d.Category AS DiagnosisCategory,
           COUNT(*) AS TestCount,
           SUM(lr.IsAbnormal) AS AbnormalCount,
           ROUND(100.0 * SUM(lr.IsAbnormal) / COUNT(*), 2) AS AbnormalPercent,
           ROUND(AVG(lr.ResultValue), 2) AS AverageResult,
           ROUND(AVG(lr.ReferenceMax), 2) AS AverageReferenceMaximum
    FROM LabResults lr
    JOIN Visits v ON v.Id = lr.VisitId
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    WHERE v.Status = 'Completed'
    GROUP BY lr.TestName, d.Category
    HAVING COUNT(*) >= 5
    ORDER BY AbnormalPercent DESC, TestCount DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) lr.TestName, d.Category AS DiagnosisCategory,
           COUNT(*) AS TestCount,
           SUM(CAST(lr.IsAbnormal AS int)) AS AbnormalCount,
           ROUND(100.0 * SUM(CAST(lr.IsAbnormal AS int)) / COUNT(*), 2) AS AbnormalPercent,
           ROUND(AVG(lr.ResultValue), 2) AS AverageResult,
           ROUND(AVG(lr.ReferenceMax), 2) AS AverageReferenceMaximum
    FROM LabResults lr
    JOIN Visits v ON v.Id = lr.VisitId
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    WHERE v.Status = 'Completed'
    GROUP BY lr.TestName, d.Category
    HAVING COUNT(*) >= 5
    ORDER BY AbnormalPercent DESC, TestCount DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT lr.TestName, d.Category AS DiagnosisCategory,
           COUNT(*) AS TestCount,
           SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount,
           ROUND(100.0 * SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) / COUNT(*), 2) AS AbnormalPercent,
           ROUND(AVG(lr.ResultValue), 2) AS AverageResult,
           ROUND(AVG(lr.ReferenceMax), 2) AS AverageReferenceMaximum
    FROM LabResults lr
    JOIN Visits v ON v.Id = lr.VisitId
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    WHERE v.Status = 'Completed'
    GROUP BY lr.TestName, d.Category
    HAVING COUNT(*) >= 5
    ORDER BY AbnormalPercent DESC, TestCount DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT lr.TestName, d.Category AS DiagnosisCategory,
           COUNT(*) AS TestCount,
           SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount,
           ROUND(100.0 * SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) / COUNT(*), 2) AS AbnormalPercent,
           ROUND(AVG(lr.ResultValue), 2) AS AverageResult,
           ROUND(AVG(lr.ReferenceMax), 2) AS AverageReferenceMaximum
    FROM LabResults lr
    JOIN Visits v ON v.Id = lr.VisitId
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    WHERE v.Status = 'Completed'
    GROUP BY lr.TestName, d.Category
    HAVING COUNT(*) >= 5
    ORDER BY AbnormalPercent DESC, TestCount DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT lr.TestName, d.Category AS DiagnosisCategory,
           COUNT(*) AS TestCount,
           SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount,
           ROUND(100.0 * SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) / COUNT(*), 2) AS AbnormalPercent,
           ROUND(AVG(lr.ResultValue), 2) AS AverageResult,
           ROUND(AVG(lr.ReferenceMax), 2) AS AverageReferenceMaximum
    FROM LabResults lr
    JOIN Visits v ON v.Id = lr.VisitId
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    WHERE v.Status = 'Completed'
    GROUP BY lr.TestName, d.Category
    HAVING COUNT(*) >= 5
    ORDER BY AbnormalPercent DESC, TestCount DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT lr.TestName, d.Category AS DiagnosisCategory,
           COUNT(*) AS TestCount,
           SUM(CASE WHEN lr.IsAbnormal = 1 THEN 1 ELSE 0 END) AS AbnormalCount,
           ROUND(100.0 * SUM(CASE WHEN lr.IsAbnormal = 1 THEN 1 ELSE 0 END) / COUNT(*), 2) AS AbnormalPercent,
           ROUND(AVG(lr.ResultValue), 2) AS AverageResult,
           ROUND(AVG(lr.ReferenceMax), 2) AS AverageReferenceMaximum
    FROM LabResults lr
    JOIN Visits v ON v.Id = lr.VisitId
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    WHERE v.Status = 'Completed'
    GROUP BY lr.TestName, d.Category
    HAVING COUNT(*) >= 5
    ORDER BY AbnormalPercent DESC, TestCount DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT lr.TestName, d.Category AS DiagnosisCategory,
           COUNT(*) AS TestCount,
           SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount,
           ROUND(100.0 * SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) / COUNT(*), 2) AS AbnormalPercent,
           ROUND(AVG(lr.ResultValue), 2) AS AverageResult,
           ROUND(AVG(lr.ReferenceMax), 2) AS AverageReferenceMaximum
    FROM LabResults lr
    JOIN Visits v ON v.Id = lr.VisitId
    JOIN Diagnoses d ON d.Id = v.DiagnosisId
    WHERE v.Status = 'Completed'
    GROUP BY lr.TestName, d.Category
    HAVING COUNT(*) >= 5
    ORDER BY AbnormalPercent DESC, TestCount DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT lr.TestName, d.Category AS DiagnosisCategory,

               COUNT(*) AS TestCount,

               SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) AS AbnormalCount,

               ROUND(100.0 * SUM(CASE WHEN lr.IsAbnormal THEN 1 ELSE 0 END) / COUNT(*), 2) AS AbnormalPercent,

               ROUND(AVG(lr.ResultValue), 2) AS AverageResult,

               ROUND(AVG(lr.ReferenceMax), 2) AS AverageReferenceMaximum

        FROM HE.LabResults lr

        JOIN HE.Visits v ON v.Id = lr.VisitId

        JOIN HE.Diagnoses d ON d.Id = v.DiagnosisId

        WHERE v.Status = 'Completed'

        GROUP BY lr.TestName, d.Category

        HAVING COUNT(*) >= 5

        ORDER BY AbnormalPercent DESC, TestCount DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 5. Visit-type utilization by month

Compare demand, no-shows, average duration, and charges for monthly care
operations.

=== "SQLite"

    ```sql
    SELECT strftime('%Y-%m', VisitDate) AS VisitMonth, VisitType,
           COUNT(*) AS ScheduledVisits,
           SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END) AS NoShows,
           ROUND(100.0 * SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END)
                 / COUNT(*), 2) AS NoShowPercent,
           ROUND(AVG(CASE WHEN Status = 'Completed' THEN DurationMinutes END), 1) AS AverageMinutes,
           ROUND(SUM(Charge), 2) AS Charges
    FROM Visits
    GROUP BY VisitMonth, VisitType
    ORDER BY ScheduledVisits DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) CONVERT(char(7), VisitDate, 126) AS VisitMonth, VisitType,
           COUNT(*) AS ScheduledVisits,
           SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END) AS NoShows,
           ROUND(100.0 * SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END)
                 / COUNT(*), 2) AS NoShowPercent,
           ROUND(AVG(CASE WHEN Status = 'Completed' THEN DurationMinutes END), 1) AS AverageMinutes,
           ROUND(SUM(Charge), 2) AS Charges
    FROM Visits
    GROUP BY VisitMonth, VisitType
    ORDER BY ScheduledVisits DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT TO_CHAR(VisitDate, 'YYYY-MM') AS VisitMonth, VisitType,
           COUNT(*) AS ScheduledVisits,
           SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END) AS NoShows,
           ROUND(100.0 * SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END)
                 / COUNT(*), 2) AS NoShowPercent,
           ROUND(AVG(CASE WHEN Status = 'Completed' THEN DurationMinutes END), 1) AS AverageMinutes,
           ROUND(SUM(Charge), 2) AS Charges
    FROM Visits
    GROUP BY VisitMonth, VisitType
    ORDER BY ScheduledVisits DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT TO_CHAR(VisitDate, 'YYYY-MM') AS VisitMonth, VisitType,
           COUNT(*) AS ScheduledVisits,
           SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END) AS NoShows,
           ROUND(100.0 * SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END)
                 / COUNT(*), 2) AS NoShowPercent,
           ROUND(AVG(CASE WHEN Status = 'Completed' THEN DurationMinutes END), 1) AS AverageMinutes,
           ROUND(SUM(Charge), 2) AS Charges
    FROM Visits
    GROUP BY VisitMonth, VisitType
    ORDER BY ScheduledVisits DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT DATE_FORMAT(VisitDate, '%Y-%m') AS VisitMonth, VisitType,
           COUNT(*) AS ScheduledVisits,
           SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END) AS NoShows,
           ROUND(100.0 * SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END)
                 / COUNT(*), 2) AS NoShowPercent,
           ROUND(AVG(CASE WHEN Status = 'Completed' THEN DurationMinutes END), 1) AS AverageMinutes,
           ROUND(SUM(Charge), 2) AS Charges
    FROM Visits
    GROUP BY VisitMonth, VisitType
    ORDER BY ScheduledVisits DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT TO_VARCHAR(VisitDate, 'YYYY-MM') AS VisitMonth, VisitType,
           COUNT(*) AS ScheduledVisits,
           SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END) AS NoShows,
           ROUND(100.0 * SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END)
                 / COUNT(*), 2) AS NoShowPercent,
           ROUND(AVG(CASE WHEN Status = 'Completed' THEN DurationMinutes END), 1) AS AverageMinutes,
           ROUND(SUM(Charge), 2) AS Charges
    FROM Visits
    GROUP BY TO_VARCHAR(VisitDate, 'YYYY-MM'), VisitType
    ORDER BY ScheduledVisits DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT SUBSTRING(CAST(VisitDate AS VARCHAR(10)) FROM 1 FOR 7) AS VisitMonth, VisitType,
           COUNT(*) AS ScheduledVisits,
           SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END) AS NoShows,
           ROUND(100.0 * SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END)
                 / COUNT(*), 2) AS NoShowPercent,
           ROUND(AVG(CASE WHEN Status = 'Completed' THEN DurationMinutes END), 1) AS AverageMinutes,
           ROUND(SUM(Charge), 2) AS Charges
    FROM Visits
    GROUP BY VisitMonth, VisitType
    ORDER BY ScheduledVisits DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT VARCHAR_FORMAT(VisitDate, 'YYYY-MM') AS VisitMonth, VisitType,

               COUNT(*) AS ScheduledVisits,

               SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END) AS NoShows,

               ROUND(100.0 * SUM(CASE WHEN Status = 'No-Show' THEN 1 ELSE 0 END)

                     / COUNT(*), 2) AS NoShowPercent,

               ROUND(AVG(CASE WHEN Status = 'Completed' THEN DurationMinutes END), 1) AS AverageMinutes,

               ROUND(SUM(Charge), 2) AS Charges

        FROM HE.Visits

        GROUP BY VARCHAR_FORMAT(VisitDate, 'YYYY-MM'), VisitType

        ORDER BY ScheduledVisits DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

## Insurance

### 1. Denial analysis by plan and reason

Show claim counts and billed/allowed exposure for each plan tier and denial
reason.

=== "SQLite"

    ```sql
    SELECT ip.Tier, ip.Name AS Plan,
           c.DenialReason,
           COUNT(*) AS DeniedClaims,
           ROUND(SUM(c.BilledAmount), 2) AS BilledAmount,
           ROUND(SUM(c.AllowedAmount), 2) AS AllowedAmount,
           ROUND(AVG(c.BilledAmount), 2) AS AverageBilledAmount
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    WHERE c.Status = 'Denied'
    GROUP BY ip.Tier, ip.Name, c.DenialReason
    ORDER BY DeniedClaims DESC, BilledAmount DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) ip.Tier, ip.Name AS Plan,
           c.DenialReason,
           COUNT(*) AS DeniedClaims,
           ROUND(SUM(c.BilledAmount), 2) AS BilledAmount,
           ROUND(SUM(c.AllowedAmount), 2) AS AllowedAmount,
           ROUND(AVG(c.BilledAmount), 2) AS AverageBilledAmount
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    WHERE c.Status = 'Denied'
    GROUP BY ip.Tier, ip.Name, c.DenialReason
    ORDER BY DeniedClaims DESC, BilledAmount DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT ip.Tier, ip.Name AS Plan,
           c.DenialReason,
           COUNT(*) AS DeniedClaims,
           ROUND(SUM(c.BilledAmount), 2) AS BilledAmount,
           ROUND(SUM(c.AllowedAmount), 2) AS AllowedAmount,
           ROUND(AVG(c.BilledAmount), 2) AS AverageBilledAmount
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    WHERE c.Status = 'Denied'
    GROUP BY ip.Tier, ip.Name, c.DenialReason
    ORDER BY DeniedClaims DESC, BilledAmount DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT ip.Tier, ip.Name AS Plan,
           c.DenialReason,
           COUNT(*) AS DeniedClaims,
           ROUND(SUM(c.BilledAmount), 2) AS BilledAmount,
           ROUND(SUM(c.AllowedAmount), 2) AS AllowedAmount,
           ROUND(AVG(c.BilledAmount), 2) AS AverageBilledAmount
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    WHERE c.Status = 'Denied'
    GROUP BY ip.Tier, ip.Name, c.DenialReason
    ORDER BY DeniedClaims DESC, BilledAmount DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT ip.Tier, ip.Name AS Plan,
           c.DenialReason,
           COUNT(*) AS DeniedClaims,
           ROUND(SUM(c.BilledAmount), 2) AS BilledAmount,
           ROUND(SUM(c.AllowedAmount), 2) AS AllowedAmount,
           ROUND(AVG(c.BilledAmount), 2) AS AverageBilledAmount
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    WHERE c.Status = 'Denied'
    GROUP BY ip.Tier, ip.Name, c.DenialReason
    ORDER BY DeniedClaims DESC, BilledAmount DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT ip.Tier, ip.Name AS Plan,
           c.DenialReason,
           COUNT(*) AS DeniedClaims,
           ROUND(SUM(c.BilledAmount), 2) AS BilledAmount,
           ROUND(SUM(c.AllowedAmount), 2) AS AllowedAmount,
           ROUND(AVG(c.BilledAmount), 2) AS AverageBilledAmount
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    WHERE c.Status = 'Denied'
    GROUP BY ip.Tier, ip.Name, c.DenialReason
    ORDER BY DeniedClaims DESC, BilledAmount DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT ip.Tier, ip.Name AS Plan,
           c.DenialReason,
           COUNT(*) AS DeniedClaims,
           ROUND(SUM(c.BilledAmount), 2) AS BilledAmount,
           ROUND(SUM(c.AllowedAmount), 2) AS AllowedAmount,
           ROUND(AVG(c.BilledAmount), 2) AS AverageBilledAmount
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    WHERE c.Status = 'Denied'
    GROUP BY ip.Tier, ip.Name, c.DenialReason
    ORDER BY DeniedClaims DESC, BilledAmount DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT ip.Tier, ip.Name AS Plan,

               c.DenialReason,

               COUNT(*) AS DeniedClaims,

               ROUND(SUM(c.BilledAmount), 2) AS BilledAmount,

               ROUND(SUM(c.AllowedAmount), 2) AS AllowedAmount,

               ROUND(AVG(c.BilledAmount), 2) AS AverageBilledAmount

        FROM IN.Claims c

        JOIN IN.Policies po ON po.Id = c.PolicyId

        JOIN IN.InsurancePlans ip ON ip.Id = po.PlanId

        WHERE c.Status = 'Denied'

        GROUP BY ip.Tier, ip.Name, c.DenialReason

        ORDER BY DeniedClaims DESC, BilledAmount DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 2. Plan profitability and medical loss ratio

Pre-aggregate premiums and claims to avoid multiplying amounts across
one-to-many joins. Compare paid premium revenue with insurer-paid claims.

=== "SQLite"

    ```sql
    WITH premium_totals AS (
        SELECT po.PlanId, SUM(pp.Amount) AS PremiumRevenue,
               COUNT(CASE WHEN pp.Status = 'Paid' THEN 1 END) AS PaidInstallments,
               COUNT(*) AS Installments
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.PlanId
    ),
    claim_totals AS (
        SELECT po.PlanId, SUM(c.PaidAmount) AS ClaimsPaid,
               COUNT(c.Id) AS ClaimCount,
               COUNT(CASE WHEN c.Status = 'Denied' THEN 1 END) AS DeniedClaims
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.PlanId
    )
    SELECT ip.Name AS Plan, ip.Tier, COUNT(DISTINCT po.Id) AS PolicyCount,
           ROUND(COALESCE(pt.PremiumRevenue, 0), 2) AS PremiumRevenue,
           COALESCE(ct.ClaimCount, 0) AS ClaimCount,
           ROUND(COALESCE(ct.ClaimsPaid, 0), 2) AS ClaimsPaid,
           ROUND(100.0 * COALESCE(ct.ClaimsPaid, 0) / NULLIF(pt.PremiumRevenue, 0), 2) AS MedicalLossRatioPercent,
           COALESCE(ct.DeniedClaims, 0) AS DeniedClaims
    FROM InsurancePlans ip
    LEFT JOIN Policies po ON po.PlanId = ip.Id
    LEFT JOIN premium_totals pt ON pt.PlanId = ip.Id
    LEFT JOIN claim_totals ct ON ct.PlanId = ip.Id
    GROUP BY ip.Id, ip.Name, ip.Tier, pt.PremiumRevenue, ct.ClaimCount, ct.ClaimsPaid, ct.DeniedClaims
    ORDER BY MedicalLossRatioPercent DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH premium_totals AS (
        SELECT po.PlanId, SUM(pp.Amount) AS PremiumRevenue,
               COUNT(CASE WHEN pp.Status = 'Paid' THEN 1 END) AS PaidInstallments,
               COUNT(*) AS Installments
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.PlanId
    ),
    claim_totals AS (
        SELECT po.PlanId, SUM(c.PaidAmount) AS ClaimsPaid,
               COUNT(c.Id) AS ClaimCount,
               COUNT(CASE WHEN c.Status = 'Denied' THEN 1 END) AS DeniedClaims
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.PlanId
    )
    SELECT TOP (10) ip.Name AS Plan, ip.Tier, COUNT(DISTINCT po.Id) AS PolicyCount,
           ROUND(COALESCE(pt.PremiumRevenue, 0), 2) AS PremiumRevenue,
           COALESCE(ct.ClaimCount, 0) AS ClaimCount,
           ROUND(COALESCE(ct.ClaimsPaid, 0), 2) AS ClaimsPaid,
           ROUND(100.0 * COALESCE(ct.ClaimsPaid, 0) / NULLIF(pt.PremiumRevenue, 0), 2) AS MedicalLossRatioPercent,
           COALESCE(ct.DeniedClaims, 0) AS DeniedClaims
    FROM InsurancePlans ip
    LEFT JOIN Policies po ON po.PlanId = ip.Id
    LEFT JOIN premium_totals pt ON pt.PlanId = ip.Id
    LEFT JOIN claim_totals ct ON ct.PlanId = ip.Id
    GROUP BY ip.Id, ip.Name, ip.Tier, pt.PremiumRevenue, ct.ClaimCount, ct.ClaimsPaid, ct.DeniedClaims
    ORDER BY MedicalLossRatioPercent DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH premium_totals AS (
        SELECT po.PlanId, SUM(pp.Amount) AS PremiumRevenue,
               COUNT(CASE WHEN pp.Status = 'Paid' THEN 1 END) AS PaidInstallments,
               COUNT(*) AS Installments
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.PlanId
    ),
    claim_totals AS (
        SELECT po.PlanId, SUM(c.PaidAmount) AS ClaimsPaid,
               COUNT(c.Id) AS ClaimCount,
               COUNT(CASE WHEN c.Status = 'Denied' THEN 1 END) AS DeniedClaims
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.PlanId
    )
    SELECT ip.Name AS Plan, ip.Tier, COUNT(DISTINCT po.Id) AS PolicyCount,
           ROUND(COALESCE(pt.PremiumRevenue, 0), 2) AS PremiumRevenue,
           COALESCE(ct.ClaimCount, 0) AS ClaimCount,
           ROUND(COALESCE(ct.ClaimsPaid, 0), 2) AS ClaimsPaid,
           ROUND(100.0 * COALESCE(ct.ClaimsPaid, 0) / NULLIF(pt.PremiumRevenue, 0), 2) AS MedicalLossRatioPercent,
           COALESCE(ct.DeniedClaims, 0) AS DeniedClaims
    FROM InsurancePlans ip
    LEFT JOIN Policies po ON po.PlanId = ip.Id
    LEFT JOIN premium_totals pt ON pt.PlanId = ip.Id
    LEFT JOIN claim_totals ct ON ct.PlanId = ip.Id
    GROUP BY ip.Id, ip.Name, ip.Tier, pt.PremiumRevenue, ct.ClaimCount, ct.ClaimsPaid, ct.DeniedClaims
    ORDER BY MedicalLossRatioPercent DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH premium_totals AS (
        SELECT po.PlanId, SUM(pp.Amount) AS PremiumRevenue,
               COUNT(CASE WHEN pp.Status = 'Paid' THEN 1 END) AS PaidInstallments,
               COUNT(*) AS Installments
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.PlanId
    ),
    claim_totals AS (
        SELECT po.PlanId, SUM(c.PaidAmount) AS ClaimsPaid,
               COUNT(c.Id) AS ClaimCount,
               COUNT(CASE WHEN c.Status = 'Denied' THEN 1 END) AS DeniedClaims
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.PlanId
    )
    SELECT ip.Name AS Plan, ip.Tier, COUNT(DISTINCT po.Id) AS PolicyCount,
           ROUND(COALESCE(pt.PremiumRevenue, 0), 2) AS PremiumRevenue,
           COALESCE(ct.ClaimCount, 0) AS ClaimCount,
           ROUND(COALESCE(ct.ClaimsPaid, 0), 2) AS ClaimsPaid,
           ROUND(100.0 * COALESCE(ct.ClaimsPaid, 0) / NULLIF(pt.PremiumRevenue, 0), 2) AS MedicalLossRatioPercent,
           COALESCE(ct.DeniedClaims, 0) AS DeniedClaims
    FROM InsurancePlans ip
    LEFT JOIN Policies po ON po.PlanId = ip.Id
    LEFT JOIN premium_totals pt ON pt.PlanId = ip.Id
    LEFT JOIN claim_totals ct ON ct.PlanId = ip.Id
    GROUP BY ip.Id, ip.Name, ip.Tier, pt.PremiumRevenue, ct.ClaimCount, ct.ClaimsPaid, ct.DeniedClaims
    ORDER BY MedicalLossRatioPercent DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH premium_totals AS (
        SELECT po.PlanId, SUM(pp.Amount) AS PremiumRevenue,
               COUNT(CASE WHEN pp.Status = 'Paid' THEN 1 END) AS PaidInstallments,
               COUNT(*) AS Installments
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.PlanId
    ),
    claim_totals AS (
        SELECT po.PlanId, SUM(c.PaidAmount) AS ClaimsPaid,
               COUNT(c.Id) AS ClaimCount,
               COUNT(CASE WHEN c.Status = 'Denied' THEN 1 END) AS DeniedClaims
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.PlanId
    )
    SELECT ip.Name AS Plan, ip.Tier, COUNT(DISTINCT po.Id) AS PolicyCount,
           ROUND(COALESCE(pt.PremiumRevenue, 0), 2) AS PremiumRevenue,
           COALESCE(ct.ClaimCount, 0) AS ClaimCount,
           ROUND(COALESCE(ct.ClaimsPaid, 0), 2) AS ClaimsPaid,
           ROUND(100.0 * COALESCE(ct.ClaimsPaid, 0) / NULLIF(pt.PremiumRevenue, 0), 2) AS MedicalLossRatioPercent,
           COALESCE(ct.DeniedClaims, 0) AS DeniedClaims
    FROM InsurancePlans ip
    LEFT JOIN Policies po ON po.PlanId = ip.Id
    LEFT JOIN premium_totals pt ON pt.PlanId = ip.Id
    LEFT JOIN claim_totals ct ON ct.PlanId = ip.Id
    GROUP BY ip.Id, ip.Name, ip.Tier, pt.PremiumRevenue, ct.ClaimCount, ct.ClaimsPaid, ct.DeniedClaims
    ORDER BY MedicalLossRatioPercent DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH premium_totals AS (
        SELECT po.PlanId, SUM(pp.Amount) AS PremiumRevenue,
               COUNT(CASE WHEN pp.Status = 'Paid' THEN 1 END) AS PaidInstallments,
               COUNT(*) AS Installments
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.PlanId
    ),
    claim_totals AS (
        SELECT po.PlanId, SUM(c.PaidAmount) AS ClaimsPaid,
               COUNT(c.Id) AS ClaimCount,
               COUNT(CASE WHEN c.Status = 'Denied' THEN 1 END) AS DeniedClaims
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.PlanId
    )
    SELECT ip.Name AS Plan, ip.Tier, COUNT(DISTINCT po.Id) AS PolicyCount,
           ROUND(COALESCE(pt.PremiumRevenue, 0), 2) AS PremiumRevenue,
           COALESCE(ct.ClaimCount, 0) AS ClaimCount,
           ROUND(COALESCE(ct.ClaimsPaid, 0), 2) AS ClaimsPaid,
           ROUND(100.0 * COALESCE(ct.ClaimsPaid, 0) / NULLIF(pt.PremiumRevenue, 0), 2) AS MedicalLossRatioPercent,
           COALESCE(ct.DeniedClaims, 0) AS DeniedClaims
    FROM InsurancePlans ip
    LEFT JOIN Policies po ON po.PlanId = ip.Id
    LEFT JOIN premium_totals pt ON pt.PlanId = ip.Id
    LEFT JOIN claim_totals ct ON ct.PlanId = ip.Id
    GROUP BY ip.Id, ip.Name, ip.Tier, pt.PremiumRevenue, ct.ClaimCount, ct.ClaimsPaid, ct.DeniedClaims
    ORDER BY MedicalLossRatioPercent DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH premium_totals AS (
        SELECT po.PlanId, SUM(pp.Amount) AS PremiumRevenue,
               COUNT(CASE WHEN pp.Status = 'Paid' THEN 1 END) AS PaidInstallments,
               COUNT(*) AS Installments
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.PlanId
    ),
    claim_totals AS (
        SELECT po.PlanId, SUM(c.PaidAmount) AS ClaimsPaid,
               COUNT(c.Id) AS ClaimCount,
               COUNT(CASE WHEN c.Status = 'Denied' THEN 1 END) AS DeniedClaims
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.PlanId
    )
    SELECT ip.Name AS Plan, ip.Tier, COUNT(DISTINCT po.Id) AS PolicyCount,
           ROUND(COALESCE(pt.PremiumRevenue, 0), 2) AS PremiumRevenue,
           COALESCE(ct.ClaimCount, 0) AS ClaimCount,
           ROUND(COALESCE(ct.ClaimsPaid, 0), 2) AS ClaimsPaid,
           ROUND(100.0 * COALESCE(ct.ClaimsPaid, 0) / NULLIF(pt.PremiumRevenue, 0), 2) AS MedicalLossRatioPercent,
           COALESCE(ct.DeniedClaims, 0) AS DeniedClaims
    FROM InsurancePlans ip
    LEFT JOIN Policies po ON po.PlanId = ip.Id
    LEFT JOIN premium_totals pt ON pt.PlanId = ip.Id
    LEFT JOIN claim_totals ct ON ct.PlanId = ip.Id
    GROUP BY ip.Id, ip.Name, ip.Tier, pt.PremiumRevenue, ct.ClaimCount, ct.ClaimsPaid, ct.DeniedClaims
    ORDER BY MedicalLossRatioPercent DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH premium_totals AS (

            SELECT po.PlanId, SUM(pp.Amount) AS PremiumRevenue,

                   COUNT(CASE WHEN pp.Status = 'Paid' THEN 1 END) AS PaidInstallments,

                   COUNT(*) AS Installments

            FROM IN.Policies po

            JOIN IN.PremiumPayments pp ON pp.PolicyId = po.Id

            WHERE pp.Status = 'Paid'

            GROUP BY po.PlanId

        ),

        claim_totals AS (

            SELECT po.PlanId, SUM(c.PaidAmount) AS ClaimsPaid,

                   COUNT(c.Id) AS ClaimCount,

                   COUNT(CASE WHEN c.Status = 'Denied' THEN 1 END) AS DeniedClaims

            FROM IN.Policies po

            JOIN IN.Claims c ON c.PolicyId = po.Id

            GROUP BY po.PlanId

        )

        SELECT ip.Name AS Plan, ip.Tier, COUNT(DISTINCT po.Id) AS PolicyCount,

               ROUND(COALESCE(pt.PremiumRevenue, 0), 2) AS PremiumRevenue,

               COALESCE(ct.ClaimCount, 0) AS ClaimCount,

               ROUND(COALESCE(ct.ClaimsPaid, 0), 2) AS ClaimsPaid,

               ROUND(100.0 * COALESCE(ct.ClaimsPaid, 0) / NULLIF(pt.PremiumRevenue, 0), 2) AS MedicalLossRatioPercent,

               COALESCE(ct.DeniedClaims, 0) AS DeniedClaims

        FROM IN.InsurancePlans ip

        LEFT JOIN IN.Policies po ON po.PlanId = ip.Id

        LEFT JOIN premium_totals pt ON pt.PlanId = ip.Id

        LEFT JOIN claim_totals ct ON ct.PlanId = ip.Id

        GROUP BY ip.Id, ip.Name, ip.Tier, pt.PremiumRevenue, ct.ClaimCount, ct.ClaimsPaid, ct.DeniedClaims

        ORDER BY MedicalLossRatioPercent DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 3. Agent portfolio and premium collection

Compare policy book size, active membership, paid premiums, and late/missed
installments across insurance agents.

=== "SQLite"

    ```sql
    WITH payment_totals AS (
        SELECT po.AgentId,
               SUM(CASE WHEN pp.Status = 'Paid' THEN pp.Amount ELSE 0 END) AS PaidPremiums,
               SUM(CASE WHEN pp.Status = 'Late' THEN 1 ELSE 0 END) AS LatePayments,
               SUM(CASE WHEN pp.Status = 'Missed' THEN 1 ELSE 0 END) AS MissedPayments
        FROM Policies po
        LEFT JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        GROUP BY po.AgentId
    )
    SELECT e.Id AS AgentId, e.FirstName || ' ' || e.LastName AS Agent,
           l.City AS OfficeCity,
           COUNT(DISTINCT po.Id) AS PoliciesSold,
           COUNT(DISTINCT CASE WHEN po.Status = 'Active' THEN po.Id END) AS ActivePolicies,
           ROUND(COALESCE(pt.PaidPremiums, 0), 2) AS PaidPremiums,
           COALESCE(pt.LatePayments, 0) AS LatePayments,
           COALESCE(pt.MissedPayments, 0) AS MissedPayments
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Policies po ON po.AgentId = e.Id
    LEFT JOIN payment_totals pt ON pt.AgentId = e.Id
    WHERE d.Name = 'Insurance Agency'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, pt.PaidPremiums, pt.LatePayments, pt.MissedPayments
    ORDER BY PaidPremiums DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH payment_totals AS (
        SELECT po.AgentId,
               SUM(CASE WHEN pp.Status = 'Paid' THEN pp.Amount ELSE 0 END) AS PaidPremiums,
               SUM(CASE WHEN pp.Status = 'Late' THEN 1 ELSE 0 END) AS LatePayments,
               SUM(CASE WHEN pp.Status = 'Missed' THEN 1 ELSE 0 END) AS MissedPayments
        FROM Policies po
        LEFT JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        GROUP BY po.AgentId
    )
    SELECT TOP (10) e.Id AS AgentId, CONCAT(e.FirstName, ' ', e.LastName) AS Agent,
           l.City AS OfficeCity,
           COUNT(DISTINCT po.Id) AS PoliciesSold,
           COUNT(DISTINCT CASE WHEN po.Status = 'Active' THEN po.Id END) AS ActivePolicies,
           ROUND(COALESCE(pt.PaidPremiums, 0), 2) AS PaidPremiums,
           COALESCE(pt.LatePayments, 0) AS LatePayments,
           COALESCE(pt.MissedPayments, 0) AS MissedPayments
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Policies po ON po.AgentId = e.Id
    LEFT JOIN payment_totals pt ON pt.AgentId = e.Id
    WHERE d.Name = 'Insurance Agency'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, pt.PaidPremiums, pt.LatePayments, pt.MissedPayments
    ORDER BY PaidPremiums DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH payment_totals AS (
        SELECT po.AgentId,
               SUM(CASE WHEN pp.Status = 'Paid' THEN pp.Amount ELSE 0 END) AS PaidPremiums,
               SUM(CASE WHEN pp.Status = 'Late' THEN 1 ELSE 0 END) AS LatePayments,
               SUM(CASE WHEN pp.Status = 'Missed' THEN 1 ELSE 0 END) AS MissedPayments
        FROM Policies po
        LEFT JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        GROUP BY po.AgentId
    )
    SELECT e.Id AS AgentId, e.FirstName || ' ' || e.LastName AS Agent,
           l.City AS OfficeCity,
           COUNT(DISTINCT po.Id) AS PoliciesSold,
           COUNT(DISTINCT CASE WHEN po.Status = 'Active' THEN po.Id END) AS ActivePolicies,
           ROUND(COALESCE(pt.PaidPremiums, 0), 2) AS PaidPremiums,
           COALESCE(pt.LatePayments, 0) AS LatePayments,
           COALESCE(pt.MissedPayments, 0) AS MissedPayments
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Policies po ON po.AgentId = e.Id
    LEFT JOIN payment_totals pt ON pt.AgentId = e.Id
    WHERE d.Name = 'Insurance Agency'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, pt.PaidPremiums, pt.LatePayments, pt.MissedPayments
    ORDER BY PaidPremiums DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH payment_totals AS (
        SELECT po.AgentId,
               SUM(CASE WHEN pp.Status = 'Paid' THEN pp.Amount ELSE 0 END) AS PaidPremiums,
               SUM(CASE WHEN pp.Status = 'Late' THEN 1 ELSE 0 END) AS LatePayments,
               SUM(CASE WHEN pp.Status = 'Missed' THEN 1 ELSE 0 END) AS MissedPayments
        FROM Policies po
        LEFT JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        GROUP BY po.AgentId
    )
    SELECT e.Id AS AgentId, e.FirstName || ' ' || e.LastName AS Agent,
           l.City AS OfficeCity,
           COUNT(DISTINCT po.Id) AS PoliciesSold,
           COUNT(DISTINCT CASE WHEN po.Status = 'Active' THEN po.Id END) AS ActivePolicies,
           ROUND(COALESCE(pt.PaidPremiums, 0), 2) AS PaidPremiums,
           COALESCE(pt.LatePayments, 0) AS LatePayments,
           COALESCE(pt.MissedPayments, 0) AS MissedPayments
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Policies po ON po.AgentId = e.Id
    LEFT JOIN payment_totals pt ON pt.AgentId = e.Id
    WHERE d.Name = 'Insurance Agency'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, pt.PaidPremiums, pt.LatePayments, pt.MissedPayments
    ORDER BY PaidPremiums DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH payment_totals AS (
        SELECT po.AgentId,
               SUM(CASE WHEN pp.Status = 'Paid' THEN pp.Amount ELSE 0 END) AS PaidPremiums,
               SUM(CASE WHEN pp.Status = 'Late' THEN 1 ELSE 0 END) AS LatePayments,
               SUM(CASE WHEN pp.Status = 'Missed' THEN 1 ELSE 0 END) AS MissedPayments
        FROM Policies po
        LEFT JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        GROUP BY po.AgentId
    )
    SELECT e.Id AS AgentId, CONCAT(e.FirstName, ' ', e.LastName) AS Agent,
           l.City AS OfficeCity,
           COUNT(DISTINCT po.Id) AS PoliciesSold,
           COUNT(DISTINCT CASE WHEN po.Status = 'Active' THEN po.Id END) AS ActivePolicies,
           ROUND(COALESCE(pt.PaidPremiums, 0), 2) AS PaidPremiums,
           COALESCE(pt.LatePayments, 0) AS LatePayments,
           COALESCE(pt.MissedPayments, 0) AS MissedPayments
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Policies po ON po.AgentId = e.Id
    LEFT JOIN payment_totals pt ON pt.AgentId = e.Id
    WHERE d.Name = 'Insurance Agency'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, pt.PaidPremiums, pt.LatePayments, pt.MissedPayments
    ORDER BY PaidPremiums DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH payment_totals AS (
        SELECT po.AgentId,
               SUM(CASE WHEN pp.Status = 'Paid' THEN pp.Amount ELSE 0 END) AS PaidPremiums,
               SUM(CASE WHEN pp.Status = 'Late' THEN 1 ELSE 0 END) AS LatePayments,
               SUM(CASE WHEN pp.Status = 'Missed' THEN 1 ELSE 0 END) AS MissedPayments
        FROM Policies po
        LEFT JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        GROUP BY po.AgentId
    )
    SELECT e.Id AS AgentId, e.FirstName || ' ' || e.LastName AS Agent,
           l.City AS OfficeCity,
           COUNT(DISTINCT po.Id) AS PoliciesSold,
           COUNT(DISTINCT CASE WHEN po.Status = 'Active' THEN po.Id END) AS ActivePolicies,
           ROUND(COALESCE(pt.PaidPremiums, 0), 2) AS PaidPremiums,
           COALESCE(pt.LatePayments, 0) AS LatePayments,
           COALESCE(pt.MissedPayments, 0) AS MissedPayments
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Policies po ON po.AgentId = e.Id
    LEFT JOIN payment_totals pt ON pt.AgentId = e.Id
    WHERE d.Name = 'Insurance Agency'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, pt.PaidPremiums, pt.LatePayments, pt.MissedPayments
    ORDER BY PaidPremiums DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH payment_totals AS (
        SELECT po.AgentId,
               SUM(CASE WHEN pp.Status = 'Paid' THEN pp.Amount ELSE 0 END) AS PaidPremiums,
               SUM(CASE WHEN pp.Status = 'Late' THEN 1 ELSE 0 END) AS LatePayments,
               SUM(CASE WHEN pp.Status = 'Missed' THEN 1 ELSE 0 END) AS MissedPayments
        FROM Policies po
        LEFT JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        GROUP BY po.AgentId
    )
    SELECT e.Id AS AgentId, e.FirstName || ' ' || e.LastName AS Agent,
           l.City AS OfficeCity,
           COUNT(DISTINCT po.Id) AS PoliciesSold,
           COUNT(DISTINCT CASE WHEN po.Status = 'Active' THEN po.Id END) AS ActivePolicies,
           ROUND(COALESCE(pt.PaidPremiums, 0), 2) AS PaidPremiums,
           COALESCE(pt.LatePayments, 0) AS LatePayments,
           COALESCE(pt.MissedPayments, 0) AS MissedPayments
    FROM Employees e
    JOIN Departments d ON d.Id = e.DepartmentId
    JOIN Locations l ON l.Id = e.LocationId
    LEFT JOIN Policies po ON po.AgentId = e.Id
    LEFT JOIN payment_totals pt ON pt.AgentId = e.Id
    WHERE d.Name = 'Insurance Agency'
    GROUP BY e.Id, e.FirstName, e.LastName, l.City, pt.PaidPremiums, pt.LatePayments, pt.MissedPayments
    ORDER BY PaidPremiums DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH payment_totals AS (

            SELECT po.AgentId,

                   SUM(CASE WHEN pp.Status = 'Paid' THEN pp.Amount ELSE 0 END) AS PaidPremiums,

                   SUM(CASE WHEN pp.Status = 'Late' THEN 1 ELSE 0 END) AS LatePayments,

                   SUM(CASE WHEN pp.Status = 'Missed' THEN 1 ELSE 0 END) AS MissedPayments

            FROM IN.Policies po

            LEFT JOIN IN.PremiumPayments pp ON pp.PolicyId = po.Id

            GROUP BY po.AgentId

        )

        SELECT e.Id AS AgentId, e.FirstName || ' ' || e.LastName AS Agent,

               l.City AS OfficeCity,

               COUNT(DISTINCT po.Id) AS PoliciesSold,

               COUNT(DISTINCT CASE WHEN po.Status = 'Active' THEN po.Id END) AS ActivePolicies,

               ROUND(COALESCE(pt.PaidPremiums, 0), 2) AS PaidPremiums,

               COALESCE(pt.LatePayments, 0) AS LatePayments,

               COALESCE(pt.MissedPayments, 0) AS MissedPayments

        FROM MAIN.Employees e

        JOIN HR.Departments d ON d.Id = e.DepartmentId

        JOIN MAIN.Locations l ON l.Id = e.LocationId

        LEFT JOIN IN.Policies po ON po.AgentId = e.Id

        LEFT JOIN payment_totals pt ON pt.AgentId = e.Id

        WHERE d.Name = 'Insurance Agency'

        GROUP BY e.Id, e.FirstName, e.LastName, l.City, pt.PaidPremiums, pt.LatePayments, pt.MissedPayments

        ORDER BY PaidPremiums DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 4. Claims adjudication by plan

Measure approval and denial rates, average processing time, and paid amounts
by plan. Pending claims are excluded from the processing-time denominator.

=== "SQLite"

    ```sql
    SELECT ip.Name AS Plan, ip.Tier,
           COUNT(*) AS ClaimsReceived,
           SUM(CASE WHEN c.Status = 'Paid' THEN 1 ELSE 0 END) AS PaidClaims,
           SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) AS DeniedClaims,
           ROUND(100.0 * SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) / COUNT(*), 2) AS DenialPercent,
           ROUND(AVG(CASE WHEN c.ProcessedDate IS NOT NULL
                          THEN julianday(c.ProcessedDate) - julianday(c.ClaimDate) END), 1) AS AverageProcessingDays,
           ROUND(SUM(c.PaidAmount), 2) AS TotalPaid
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    GROUP BY ip.Id, ip.Name, ip.Tier
    ORDER BY ClaimsReceived DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) ip.Name AS Plan, ip.Tier,
           COUNT(*) AS ClaimsReceived,
           SUM(CASE WHEN c.Status = 'Paid' THEN 1 ELSE 0 END) AS PaidClaims,
           SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) AS DeniedClaims,
           ROUND(100.0 * SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) / COUNT(*), 2) AS DenialPercent,
           ROUND(AVG(CASE WHEN c.ProcessedDate IS NOT NULL
                          THEN DATEDIFF(DAY, c.ClaimDate, c.ProcessedDate) END), 1) AS AverageProcessingDays,
           ROUND(SUM(c.PaidAmount), 2) AS TotalPaid
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    GROUP BY ip.Id, ip.Name, ip.Tier
    ORDER BY ClaimsReceived DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT ip.Name AS Plan, ip.Tier,
           COUNT(*) AS ClaimsReceived,
           SUM(CASE WHEN c.Status = 'Paid' THEN 1 ELSE 0 END) AS PaidClaims,
           SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) AS DeniedClaims,
           ROUND(100.0 * SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) / COUNT(*), 2) AS DenialPercent,
           ROUND(AVG(CASE WHEN c.ProcessedDate IS NOT NULL
                          THEN EXTRACT(EPOCH FROM (c.ProcessedDate - c.ClaimDate)) / 86400.0 END), 1) AS AverageProcessingDays,
           ROUND(SUM(c.PaidAmount), 2) AS TotalPaid
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    GROUP BY ip.Id, ip.Name, ip.Tier
    ORDER BY ClaimsReceived DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT ip.Name AS Plan, ip.Tier,
           COUNT(*) AS ClaimsReceived,
           SUM(CASE WHEN c.Status = 'Paid' THEN 1 ELSE 0 END) AS PaidClaims,
           SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) AS DeniedClaims,
           ROUND(100.0 * SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) / COUNT(*), 2) AS DenialPercent,
           ROUND(AVG(CASE WHEN c.ProcessedDate IS NOT NULL
                          THEN (c.ProcessedDate - c.ClaimDate) END), 1) AS AverageProcessingDays,
           ROUND(SUM(c.PaidAmount), 2) AS TotalPaid
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    GROUP BY ip.Id, ip.Name, ip.Tier
    ORDER BY ClaimsReceived DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT ip.Name AS Plan, ip.Tier,
           COUNT(*) AS ClaimsReceived,
           SUM(CASE WHEN c.Status = 'Paid' THEN 1 ELSE 0 END) AS PaidClaims,
           SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) AS DeniedClaims,
           ROUND(100.0 * SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) / COUNT(*), 2) AS DenialPercent,
           ROUND(AVG(CASE WHEN c.ProcessedDate IS NOT NULL
                          THEN DATEDIFF(c.ProcessedDate, c.ClaimDate) END), 1) AS AverageProcessingDays,
           ROUND(SUM(c.PaidAmount), 2) AS TotalPaid
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    GROUP BY ip.Id, ip.Name, ip.Tier
    ORDER BY ClaimsReceived DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT ip.Name AS Plan, ip.Tier,
           COUNT(*) AS ClaimsReceived,
           SUM(CASE WHEN c.Status = 'Paid' THEN 1 ELSE 0 END) AS PaidClaims,
           SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) AS DeniedClaims,
           ROUND(100.0 * SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) / COUNT(*), 2) AS DenialPercent,
           ROUND(AVG(CASE WHEN c.ProcessedDate IS NOT NULL
                          THEN DAYS_BETWEEN(c.ClaimDate, c.ProcessedDate) END), 1) AS AverageProcessingDays,
           ROUND(SUM(c.PaidAmount), 2) AS TotalPaid
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    GROUP BY ip.Id, ip.Name, ip.Tier
    ORDER BY ClaimsReceived DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT ip.Name AS Plan, ip.Tier,
           COUNT(*) AS ClaimsReceived,
           SUM(CASE WHEN c.Status = 'Paid' THEN 1 ELSE 0 END) AS PaidClaims,
           SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) AS DeniedClaims,
           ROUND(100.0 * SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) / COUNT(*), 2) AS DenialPercent,
           ROUND(AVG(CASE WHEN c.ProcessedDate IS NOT NULL
                          THEN DATEDIFF(DAY FROM c.ClaimDate TO c.ProcessedDate) END), 1) AS AverageProcessingDays,
           ROUND(SUM(c.PaidAmount), 2) AS TotalPaid
    FROM Claims c
    JOIN Policies po ON po.Id = c.PolicyId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    GROUP BY ip.Id, ip.Name, ip.Tier
    ORDER BY ClaimsReceived DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT ip.Name AS Plan, ip.Tier,

               COUNT(*) AS ClaimsReceived,

               SUM(CASE WHEN c.Status = 'Paid' THEN 1 ELSE 0 END) AS PaidClaims,

               SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) AS DeniedClaims,

               ROUND(100.0 * SUM(CASE WHEN c.Status = 'Denied' THEN 1 ELSE 0 END) / COUNT(*), 2) AS DenialPercent,

               ROUND(AVG(CASE WHEN c.ProcessedDate IS NOT NULL

                              THEN DAYS(c.ProcessedDate) - DAYS(c.ClaimDate) END), 1) AS AverageProcessingDays,

               ROUND(SUM(c.PaidAmount), 2) AS TotalPaid

        FROM IN.Claims c

        JOIN IN.Policies po ON po.Id = c.PolicyId

        JOIN IN.InsurancePlans ip ON ip.Id = po.PlanId

        GROUP BY ip.Id, ip.Name, ip.Tier

        ORDER BY ClaimsReceived DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 5. Member utilization versus premium

Compare each insured member's collected premiums with allowed and paid care
costs. Independent aggregates prevent claims and installments from inflating
each other.

=== "SQLite"

    ```sql
    WITH member_premiums AS (
        SELECT po.UserId, SUM(pp.Amount) AS PremiumsCollected
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.UserId
    ),
    member_claims AS (
        SELECT po.UserId, COUNT(c.Id) AS Claims,
               SUM(c.AllowedAmount) AS AllowedAmount,
               SUM(c.PaidAmount) AS InsurerPaid
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.UserId
    )
    SELECT pe.Id AS MemberId, pe.FirstName || ' ' || pe.LastName AS Member,
           ip.Name AS Plan, po.Status AS PolicyStatus,
           COALESCE(mc.Claims, 0) AS Claims,
           ROUND(COALESCE(mp.PremiumsCollected, 0), 2) AS PremiumsCollected,
           ROUND(COALESCE(mc.AllowedAmount, 0), 2) AS AllowedAmount,
           ROUND(COALESCE(mc.InsurerPaid, 0), 2) AS InsurerPaid,
           ROUND(100.0 * COALESCE(mc.InsurerPaid, 0)
                 / NULLIF(mp.PremiumsCollected, 0), 2) AS MemberLossRatioPercent
    FROM Policies po
    JOIN Users pe ON pe.Id = po.UserId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    LEFT JOIN member_premiums mp ON mp.UserId = pe.Id
    LEFT JOIN member_claims mc ON mc.UserId = pe.Id
    ORDER BY MemberLossRatioPercent DESC, Claims DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH member_premiums AS (
        SELECT po.UserId, SUM(pp.Amount) AS PremiumsCollected
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.UserId
    ),
    member_claims AS (
        SELECT po.UserId, COUNT(c.Id) AS Claims,
               SUM(c.AllowedAmount) AS AllowedAmount,
               SUM(c.PaidAmount) AS InsurerPaid
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.UserId
    )
    SELECT TOP (10) pe.Id AS MemberId, CONCAT(pe.FirstName, ' ', pe.LastName) AS Member,
           ip.Name AS Plan, po.Status AS PolicyStatus,
           COALESCE(mc.Claims, 0) AS Claims,
           ROUND(COALESCE(mp.PremiumsCollected, 0), 2) AS PremiumsCollected,
           ROUND(COALESCE(mc.AllowedAmount, 0), 2) AS AllowedAmount,
           ROUND(COALESCE(mc.InsurerPaid, 0), 2) AS InsurerPaid,
           ROUND(100.0 * COALESCE(mc.InsurerPaid, 0)
                 / NULLIF(mp.PremiumsCollected, 0), 2) AS MemberLossRatioPercent
    FROM Policies po
    JOIN Users pe ON pe.Id = po.UserId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    LEFT JOIN member_premiums mp ON mp.UserId = pe.Id
    LEFT JOIN member_claims mc ON mc.UserId = pe.Id
    ORDER BY MemberLossRatioPercent DESC, Claims DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH member_premiums AS (
        SELECT po.UserId, SUM(pp.Amount) AS PremiumsCollected
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.UserId
    ),
    member_claims AS (
        SELECT po.UserId, COUNT(c.Id) AS Claims,
               SUM(c.AllowedAmount) AS AllowedAmount,
               SUM(c.PaidAmount) AS InsurerPaid
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.UserId
    )
    SELECT pe.Id AS MemberId, pe.FirstName || ' ' || pe.LastName AS Member,
           ip.Name AS Plan, po.Status AS PolicyStatus,
           COALESCE(mc.Claims, 0) AS Claims,
           ROUND(COALESCE(mp.PremiumsCollected, 0), 2) AS PremiumsCollected,
           ROUND(COALESCE(mc.AllowedAmount, 0), 2) AS AllowedAmount,
           ROUND(COALESCE(mc.InsurerPaid, 0), 2) AS InsurerPaid,
           ROUND(100.0 * COALESCE(mc.InsurerPaid, 0)
                 / NULLIF(mp.PremiumsCollected, 0), 2) AS MemberLossRatioPercent
    FROM Policies po
    JOIN Users pe ON pe.Id = po.UserId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    LEFT JOIN member_premiums mp ON mp.UserId = pe.Id
    LEFT JOIN member_claims mc ON mc.UserId = pe.Id
    ORDER BY MemberLossRatioPercent DESC, Claims DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH member_premiums AS (
        SELECT po.UserId, SUM(pp.Amount) AS PremiumsCollected
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.UserId
    ),
    member_claims AS (
        SELECT po.UserId, COUNT(c.Id) AS Claims,
               SUM(c.AllowedAmount) AS AllowedAmount,
               SUM(c.PaidAmount) AS InsurerPaid
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.UserId
    )
    SELECT pe.Id AS MemberId, pe.FirstName || ' ' || pe.LastName AS Member,
           ip.Name AS Plan, po.Status AS PolicyStatus,
           COALESCE(mc.Claims, 0) AS Claims,
           ROUND(COALESCE(mp.PremiumsCollected, 0), 2) AS PremiumsCollected,
           ROUND(COALESCE(mc.AllowedAmount, 0), 2) AS AllowedAmount,
           ROUND(COALESCE(mc.InsurerPaid, 0), 2) AS InsurerPaid,
           ROUND(100.0 * COALESCE(mc.InsurerPaid, 0)
                 / NULLIF(mp.PremiumsCollected, 0), 2) AS MemberLossRatioPercent
    FROM Policies po
    JOIN Users pe ON pe.Id = po.UserId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    LEFT JOIN member_premiums mp ON mp.UserId = pe.Id
    LEFT JOIN member_claims mc ON mc.UserId = pe.Id
    ORDER BY MemberLossRatioPercent DESC, Claims DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH member_premiums AS (
        SELECT po.UserId, SUM(pp.Amount) AS PremiumsCollected
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.UserId
    ),
    member_claims AS (
        SELECT po.UserId, COUNT(c.Id) AS Claims,
               SUM(c.AllowedAmount) AS AllowedAmount,
               SUM(c.PaidAmount) AS InsurerPaid
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.UserId
    )
    SELECT pe.Id AS MemberId, CONCAT(pe.FirstName, ' ', pe.LastName) AS Member,
           ip.Name AS Plan, po.Status AS PolicyStatus,
           COALESCE(mc.Claims, 0) AS Claims,
           ROUND(COALESCE(mp.PremiumsCollected, 0), 2) AS PremiumsCollected,
           ROUND(COALESCE(mc.AllowedAmount, 0), 2) AS AllowedAmount,
           ROUND(COALESCE(mc.InsurerPaid, 0), 2) AS InsurerPaid,
           ROUND(100.0 * COALESCE(mc.InsurerPaid, 0)
                 / NULLIF(mp.PremiumsCollected, 0), 2) AS MemberLossRatioPercent
    FROM Policies po
    JOIN Users pe ON pe.Id = po.UserId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    LEFT JOIN member_premiums mp ON mp.UserId = pe.Id
    LEFT JOIN member_claims mc ON mc.UserId = pe.Id
    ORDER BY MemberLossRatioPercent DESC, Claims DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH member_premiums AS (
        SELECT po.UserId, SUM(pp.Amount) AS PremiumsCollected
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.UserId
    ),
    member_claims AS (
        SELECT po.UserId, COUNT(c.Id) AS Claims,
               SUM(c.AllowedAmount) AS AllowedAmount,
               SUM(c.PaidAmount) AS InsurerPaid
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.UserId
    )
    SELECT pe.Id AS MemberId, pe.FirstName || ' ' || pe.LastName AS Member,
           ip.Name AS Plan, po.Status AS PolicyStatus,
           COALESCE(mc.Claims, 0) AS Claims,
           ROUND(COALESCE(mp.PremiumsCollected, 0), 2) AS PremiumsCollected,
           ROUND(COALESCE(mc.AllowedAmount, 0), 2) AS AllowedAmount,
           ROUND(COALESCE(mc.InsurerPaid, 0), 2) AS InsurerPaid,
           ROUND(100.0 * COALESCE(mc.InsurerPaid, 0)
                 / NULLIF(mp.PremiumsCollected, 0), 2) AS MemberLossRatioPercent
    FROM Policies po
    JOIN Users pe ON pe.Id = po.UserId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    LEFT JOIN member_premiums mp ON mp.UserId = pe.Id
    LEFT JOIN member_claims mc ON mc.UserId = pe.Id
    ORDER BY MemberLossRatioPercent DESC, Claims DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH member_premiums AS (
        SELECT po.UserId, SUM(pp.Amount) AS PremiumsCollected
        FROM Policies po
        JOIN PremiumPayments pp ON pp.PolicyId = po.Id
        WHERE pp.Status = 'Paid'
        GROUP BY po.UserId
    ),
    member_claims AS (
        SELECT po.UserId, COUNT(c.Id) AS Claims,
               SUM(c.AllowedAmount) AS AllowedAmount,
               SUM(c.PaidAmount) AS InsurerPaid
        FROM Policies po
        JOIN Claims c ON c.PolicyId = po.Id
        GROUP BY po.UserId
    )
    SELECT pe.Id AS MemberId, pe.FirstName || ' ' || pe.LastName AS Member,
           ip.Name AS Plan, po.Status AS PolicyStatus,
           COALESCE(mc.Claims, 0) AS Claims,
           ROUND(COALESCE(mp.PremiumsCollected, 0), 2) AS PremiumsCollected,
           ROUND(COALESCE(mc.AllowedAmount, 0), 2) AS AllowedAmount,
           ROUND(COALESCE(mc.InsurerPaid, 0), 2) AS InsurerPaid,
           ROUND(100.0 * COALESCE(mc.InsurerPaid, 0)
                 / NULLIF(mp.PremiumsCollected, 0), 2) AS MemberLossRatioPercent
    FROM Policies po
    JOIN Users pe ON pe.Id = po.UserId
    JOIN InsurancePlans ip ON ip.Id = po.PlanId
    LEFT JOIN member_premiums mp ON mp.UserId = pe.Id
    LEFT JOIN member_claims mc ON mc.UserId = pe.Id
    ORDER BY MemberLossRatioPercent DESC, Claims DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH member_premiums AS (

            SELECT po.UserId, SUM(pp.Amount) AS PremiumsCollected

            FROM IN.Policies po

            JOIN IN.PremiumPayments pp ON pp.PolicyId = po.Id

            WHERE pp.Status = 'Paid'

            GROUP BY po.UserId

        ),

        member_claims AS (

            SELECT po.UserId, COUNT(c.Id) AS Claims,

                   SUM(c.AllowedAmount) AS AllowedAmount,

                   SUM(c.PaidAmount) AS InsurerPaid

            FROM IN.Policies po

            JOIN IN.Claims c ON c.PolicyId = po.Id

            GROUP BY po.UserId

        )

        SELECT pe.Id AS MemberId, pe.FirstName || ' ' || pe.LastName AS Member,

               ip.Name AS Plan, po.Status AS PolicyStatus,

               COALESCE(mc.Claims, 0) AS Claims,

               ROUND(COALESCE(mp.PremiumsCollected, 0), 2) AS PremiumsCollected,

               ROUND(COALESCE(mc.AllowedAmount, 0), 2) AS AllowedAmount,

               ROUND(COALESCE(mc.InsurerPaid, 0), 2) AS InsurerPaid,

               ROUND(100.0 * COALESCE(mc.InsurerPaid, 0)

                     / NULLIF(mp.PremiumsCollected, 0), 2) AS MemberLossRatioPercent

        FROM IN.Policies po

        JOIN MAIN.Users pe ON pe.Id = po.UserId

        JOIN IN.InsurancePlans ip ON ip.Id = po.PlanId

        LEFT JOIN member_premiums mp ON mp.UserId = pe.Id

        LEFT JOIN member_claims mc ON mc.UserId = pe.Id

        ORDER BY MemberLossRatioPercent DESC, Claims DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

## Human resources

### 1. Department compensation and performance

Compare headcount, payroll, performance ratings, and bonuses by department.
Review metrics are pre-aggregated to keep employee counts and payroll accurate.

=== "SQLite"

    ```sql
    WITH review_summary AS (
        SELECT EmployeeId, AVG(Rating) AS AverageRating, SUM(BonusAmount) AS Bonuses
        FROM PerformanceReviews
        WHERE ReviewYear = 2025
        GROUP BY EmployeeId
    )
    SELECT d.Name AS Department,
           COUNT(e.Id) AS Headcount,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll,
           ROUND(AVG(rs.AverageRating), 2) AS AverageRating,
           ROUND(SUM(COALESCE(rs.Bonuses, 0)), 2) AS TotalBonuses
    FROM Departments d
    JOIN Employees e ON e.DepartmentId = d.Id
    LEFT JOIN review_summary rs ON rs.EmployeeId = e.Id
    GROUP BY d.Id, d.Name
    ORDER BY AnnualPayroll DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    WITH review_summary AS (
        SELECT EmployeeId, AVG(Rating) AS AverageRating, SUM(BonusAmount) AS Bonuses
        FROM PerformanceReviews
        WHERE ReviewYear = 2025
        GROUP BY EmployeeId
    )
    SELECT TOP (10) d.Name AS Department,
           COUNT(e.Id) AS Headcount,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll,
           ROUND(AVG(rs.AverageRating), 2) AS AverageRating,
           ROUND(SUM(COALESCE(rs.Bonuses, 0)), 2) AS TotalBonuses
    FROM Departments d
    JOIN Employees e ON e.DepartmentId = d.Id
    LEFT JOIN review_summary rs ON rs.EmployeeId = e.Id
    GROUP BY d.Id, d.Name
    ORDER BY AnnualPayroll DESC;
    ```

=== "PostgreSQL"

    ```sql
    WITH review_summary AS (
        SELECT EmployeeId, AVG(Rating) AS AverageRating, SUM(BonusAmount) AS Bonuses
        FROM PerformanceReviews
        WHERE ReviewYear = 2025
        GROUP BY EmployeeId
    )
    SELECT d.Name AS Department,
           COUNT(e.Id) AS Headcount,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll,
           ROUND(AVG(rs.AverageRating), 2) AS AverageRating,
           ROUND(SUM(COALESCE(rs.Bonuses, 0)), 2) AS TotalBonuses
    FROM Departments d
    JOIN Employees e ON e.DepartmentId = d.Id
    LEFT JOIN review_summary rs ON rs.EmployeeId = e.Id
    GROUP BY d.Id, d.Name
    ORDER BY AnnualPayroll DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    WITH review_summary AS (
        SELECT EmployeeId, AVG(Rating) AS AverageRating, SUM(BonusAmount) AS Bonuses
        FROM PerformanceReviews
        WHERE ReviewYear = 2025
        GROUP BY EmployeeId
    )
    SELECT d.Name AS Department,
           COUNT(e.Id) AS Headcount,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll,
           ROUND(AVG(rs.AverageRating), 2) AS AverageRating,
           ROUND(SUM(COALESCE(rs.Bonuses, 0)), 2) AS TotalBonuses
    FROM Departments d
    JOIN Employees e ON e.DepartmentId = d.Id
    LEFT JOIN review_summary rs ON rs.EmployeeId = e.Id
    GROUP BY d.Id, d.Name
    ORDER BY AnnualPayroll DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    WITH review_summary AS (
        SELECT EmployeeId, AVG(Rating) AS AverageRating, SUM(BonusAmount) AS Bonuses
        FROM PerformanceReviews
        WHERE ReviewYear = 2025
        GROUP BY EmployeeId
    )
    SELECT d.Name AS Department,
           COUNT(e.Id) AS Headcount,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll,
           ROUND(AVG(rs.AverageRating), 2) AS AverageRating,
           ROUND(SUM(COALESCE(rs.Bonuses, 0)), 2) AS TotalBonuses
    FROM Departments d
    JOIN Employees e ON e.DepartmentId = d.Id
    LEFT JOIN review_summary rs ON rs.EmployeeId = e.Id
    GROUP BY d.Id, d.Name
    ORDER BY AnnualPayroll DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    WITH review_summary AS (
        SELECT EmployeeId, AVG(Rating) AS AverageRating, SUM(BonusAmount) AS Bonuses
        FROM PerformanceReviews
        WHERE ReviewYear = 2025
        GROUP BY EmployeeId
    )
    SELECT d.Name AS Department,
           COUNT(e.Id) AS Headcount,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll,
           ROUND(AVG(rs.AverageRating), 2) AS AverageRating,
           ROUND(SUM(COALESCE(rs.Bonuses, 0)), 2) AS TotalBonuses
    FROM Departments d
    JOIN Employees e ON e.DepartmentId = d.Id
    LEFT JOIN review_summary rs ON rs.EmployeeId = e.Id
    GROUP BY d.Id, d.Name
    ORDER BY AnnualPayroll DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    WITH review_summary AS (
        SELECT EmployeeId, AVG(Rating) AS AverageRating, SUM(BonusAmount) AS Bonuses
        FROM PerformanceReviews
        WHERE ReviewYear = 2025
        GROUP BY EmployeeId
    )
    SELECT d.Name AS Department,
           COUNT(e.Id) AS Headcount,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll,
           ROUND(AVG(rs.AverageRating), 2) AS AverageRating,
           ROUND(SUM(COALESCE(rs.Bonuses, 0)), 2) AS TotalBonuses
    FROM Departments d
    JOIN Employees e ON e.DepartmentId = d.Id
    LEFT JOIN review_summary rs ON rs.EmployeeId = e.Id
    GROUP BY d.Id, d.Name
    ORDER BY AnnualPayroll DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        WITH review_summary AS (

            SELECT EmployeeId, AVG(Rating) AS AverageRating, SUM(BonusAmount) AS Bonuses

            FROM HR.PerformanceReviews

            WHERE ReviewYear = 2025

            GROUP BY EmployeeId

        )

        SELECT d.Name AS Department,

               COUNT(e.Id) AS Headcount,

               ROUND(AVG(e.Salary), 2) AS AverageSalary,

               ROUND(SUM(e.Salary), 2) AS AnnualPayroll,

               ROUND(AVG(rs.AverageRating), 2) AS AverageRating,

               ROUND(SUM(COALESCE(rs.Bonuses, 0)), 2) AS TotalBonuses

        FROM HR.Departments d

        JOIN MAIN.Employees e ON e.DepartmentId = d.Id

        LEFT JOIN review_summary rs ON rs.EmployeeId = e.Id

        GROUP BY d.Id, d.Name

        ORDER BY AnnualPayroll DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 2. Manager span and team profile

Use a self-join to build manager teams and report span of control, average
salary, high performers, and location coverage.

=== "SQLite"

    ```sql
    SELECT manager.Id AS ManagerId,
           manager.FirstName || ' ' || manager.LastName AS Manager,
           md.Name AS Department,
           COUNT(report.Id) AS DirectReports,
           COUNT(DISTINCT report.LocationId) AS TeamLocations,
           ROUND(AVG(report.Salary), 2) AS AverageTeamSalary,
           ROUND(AVG(pr.Rating), 2) AS AverageTeamRating,
           ROUND(SUM(report.Salary), 2) AS TeamPayroll
    FROM Employees manager
    JOIN Employees report ON report.ManagerId = manager.Id
    JOIN Departments md ON md.Id = manager.DepartmentId
    LEFT JOIN PerformanceReviews pr ON pr.EmployeeId = report.Id AND pr.ReviewYear = 2025
    GROUP BY manager.Id, manager.FirstName, manager.LastName, md.Name
    ORDER BY DirectReports DESC, TeamPayroll DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) manager.Id AS ManagerId,
           CONCAT(manager.FirstName, ' ', manager.LastName) AS Manager,
           md.Name AS Department,
           COUNT(report.Id) AS DirectReports,
           COUNT(DISTINCT report.LocationId) AS TeamLocations,
           ROUND(AVG(report.Salary), 2) AS AverageTeamSalary,
           ROUND(AVG(pr.Rating), 2) AS AverageTeamRating,
           ROUND(SUM(report.Salary), 2) AS TeamPayroll
    FROM Employees manager
    JOIN Employees report ON report.ManagerId = manager.Id
    JOIN Departments md ON md.Id = manager.DepartmentId
    LEFT JOIN PerformanceReviews pr ON pr.EmployeeId = report.Id AND pr.ReviewYear = 2025
    GROUP BY manager.Id, manager.FirstName, manager.LastName, md.Name
    ORDER BY DirectReports DESC, TeamPayroll DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT manager.Id AS ManagerId,
           manager.FirstName || ' ' || manager.LastName AS Manager,
           md.Name AS Department,
           COUNT(report.Id) AS DirectReports,
           COUNT(DISTINCT report.LocationId) AS TeamLocations,
           ROUND(AVG(report.Salary), 2) AS AverageTeamSalary,
           ROUND(AVG(pr.Rating), 2) AS AverageTeamRating,
           ROUND(SUM(report.Salary), 2) AS TeamPayroll
    FROM Employees manager
    JOIN Employees report ON report.ManagerId = manager.Id
    JOIN Departments md ON md.Id = manager.DepartmentId
    LEFT JOIN PerformanceReviews pr ON pr.EmployeeId = report.Id AND pr.ReviewYear = 2025
    GROUP BY manager.Id, manager.FirstName, manager.LastName, md.Name
    ORDER BY DirectReports DESC, TeamPayroll DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT manager.Id AS ManagerId,
           manager.FirstName || ' ' || manager.LastName AS Manager,
           md.Name AS Department,
           COUNT(report.Id) AS DirectReports,
           COUNT(DISTINCT report.LocationId) AS TeamLocations,
           ROUND(AVG(report.Salary), 2) AS AverageTeamSalary,
           ROUND(AVG(pr.Rating), 2) AS AverageTeamRating,
           ROUND(SUM(report.Salary), 2) AS TeamPayroll
    FROM Employees manager
    JOIN Employees report ON report.ManagerId = manager.Id
    JOIN Departments md ON md.Id = manager.DepartmentId
    LEFT JOIN PerformanceReviews pr ON pr.EmployeeId = report.Id AND pr.ReviewYear = 2025
    GROUP BY manager.Id, manager.FirstName, manager.LastName, md.Name
    ORDER BY DirectReports DESC, TeamPayroll DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT manager.Id AS ManagerId,
           CONCAT(manager.FirstName, ' ', manager.LastName) AS Manager,
           md.Name AS Department,
           COUNT(report.Id) AS DirectReports,
           COUNT(DISTINCT report.LocationId) AS TeamLocations,
           ROUND(AVG(report.Salary), 2) AS AverageTeamSalary,
           ROUND(AVG(pr.Rating), 2) AS AverageTeamRating,
           ROUND(SUM(report.Salary), 2) AS TeamPayroll
    FROM Employees manager
    JOIN Employees report ON report.ManagerId = manager.Id
    JOIN Departments md ON md.Id = manager.DepartmentId
    LEFT JOIN PerformanceReviews pr ON pr.EmployeeId = report.Id AND pr.ReviewYear = 2025
    GROUP BY manager.Id, manager.FirstName, manager.LastName, md.Name
    ORDER BY DirectReports DESC, TeamPayroll DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT manager.Id AS ManagerId,
           manager.FirstName || ' ' || manager.LastName AS Manager,
           md.Name AS Department,
           COUNT(report.Id) AS DirectReports,
           COUNT(DISTINCT report.LocationId) AS TeamLocations,
           ROUND(AVG(report.Salary), 2) AS AverageTeamSalary,
           ROUND(AVG(pr.Rating), 2) AS AverageTeamRating,
           ROUND(SUM(report.Salary), 2) AS TeamPayroll
    FROM Employees manager
    JOIN Employees report ON report.ManagerId = manager.Id
    JOIN Departments md ON md.Id = manager.DepartmentId
    LEFT JOIN PerformanceReviews pr ON pr.EmployeeId = report.Id AND pr.ReviewYear = 2025
    GROUP BY manager.Id, manager.FirstName, manager.LastName, md.Name
    ORDER BY DirectReports DESC, TeamPayroll DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT manager.Id AS ManagerId,
           manager.FirstName || ' ' || manager.LastName AS Manager,
           md.Name AS Department,
           COUNT(report.Id) AS DirectReports,
           COUNT(DISTINCT report.LocationId) AS TeamLocations,
           ROUND(AVG(report.Salary), 2) AS AverageTeamSalary,
           ROUND(AVG(pr.Rating), 2) AS AverageTeamRating,
           ROUND(SUM(report.Salary), 2) AS TeamPayroll
    FROM Employees manager
    JOIN Employees report ON report.ManagerId = manager.Id
    JOIN Departments md ON md.Id = manager.DepartmentId
    LEFT JOIN PerformanceReviews pr ON pr.EmployeeId = report.Id AND pr.ReviewYear = 2025
    GROUP BY manager.Id, manager.FirstName, manager.LastName, md.Name
    ORDER BY DirectReports DESC, TeamPayroll DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT manager.Id AS ManagerId,

               manager.FirstName || ' ' || manager.LastName AS Manager,

               md.Name AS Department,

               COUNT(report.Id) AS DirectReports,

               COUNT(DISTINCT report.LocationId) AS TeamLocations,

               ROUND(AVG(report.Salary), 2) AS AverageTeamSalary,

               ROUND(AVG(pr.Rating), 2) AS AverageTeamRating,

               ROUND(SUM(report.Salary), 2) AS TeamPayroll

        FROM MAIN.Employees manager

        JOIN MAIN.Employees report ON report.ManagerId = manager.Id

        JOIN HR.Departments md ON md.Id = manager.DepartmentId

        LEFT JOIN HR.PerformanceReviews pr ON pr.EmployeeId = report.Id AND pr.ReviewYear = 2025

        GROUP BY manager.Id, manager.FirstName, manager.LastName, md.Name

        ORDER BY DirectReports DESC, TeamPayroll DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 3. Workforce composition by location

Show staffing mix and tenure by office, with payroll and department diversity.

=== "SQLite"

    ```sql
    SELECT l.City AS OfficeCity, l.State,
           COUNT(e.Id) AS Headcount,
           COUNT(DISTINCT e.DepartmentId) AS DepartmentsRepresented,
           SUM(CASE WHEN e.EmploymentType = 'Full-time' THEN 1 ELSE 0 END) AS FullTime,
           SUM(CASE WHEN e.EmploymentType = 'Part-time' THEN 1 ELSE 0 END) AS PartTime,
           SUM(CASE WHEN e.EmploymentType = 'Contract' THEN 1 ELSE 0 END) AS Contractors,
           ROUND(AVG((julianday('2025-10-31') - julianday(e.HireDate)) / 365.25), 1) AS AverageTenureYears,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll
    FROM Locations l
    JOIN Employees e ON e.LocationId = l.Id
    GROUP BY l.Id, l.City, l.State
    ORDER BY Headcount DESC, AnnualPayroll DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) l.City AS OfficeCity, l.State,
           COUNT(e.Id) AS Headcount,
           COUNT(DISTINCT e.DepartmentId) AS DepartmentsRepresented,
           SUM(CASE WHEN e.EmploymentType = 'Full-time' THEN 1 ELSE 0 END) AS FullTime,
           SUM(CASE WHEN e.EmploymentType = 'Part-time' THEN 1 ELSE 0 END) AS PartTime,
           SUM(CASE WHEN e.EmploymentType = 'Contract' THEN 1 ELSE 0 END) AS Contractors,
           ROUND(AVG(DATEDIFF(DAY, e.HireDate, CONVERT(date, '2025-10-31')) / 365.25), 1) AS AverageTenureYears,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll
    FROM Locations l
    JOIN Employees e ON e.LocationId = l.Id
    GROUP BY l.Id, l.City, l.State
    ORDER BY Headcount DESC, AnnualPayroll DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT l.City AS OfficeCity, l.State,
           COUNT(e.Id) AS Headcount,
           COUNT(DISTINCT e.DepartmentId) AS DepartmentsRepresented,
           SUM(CASE WHEN e.EmploymentType = 'Full-time' THEN 1 ELSE 0 END) AS FullTime,
           SUM(CASE WHEN e.EmploymentType = 'Part-time' THEN 1 ELSE 0 END) AS PartTime,
           SUM(CASE WHEN e.EmploymentType = 'Contract' THEN 1 ELSE 0 END) AS Contractors,
           ROUND(AVG((DAYS(DATE '2025-10-31') - DAYS(e.HireDate)) / 365.25), 1) AS AverageTenureYears,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll
    FROM Locations l
    JOIN Employees e ON e.LocationId = l.Id
    GROUP BY l.Id, l.City, l.State
    ORDER BY Headcount DESC, AnnualPayroll DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT l.City AS OfficeCity, l.State,
           COUNT(e.Id) AS Headcount,
           COUNT(DISTINCT e.DepartmentId) AS DepartmentsRepresented,
           SUM(CASE WHEN e.EmploymentType = 'Full-time' THEN 1 ELSE 0 END) AS FullTime,
           SUM(CASE WHEN e.EmploymentType = 'Part-time' THEN 1 ELSE 0 END) AS PartTime,
           SUM(CASE WHEN e.EmploymentType = 'Contract' THEN 1 ELSE 0 END) AS Contractors,
           ROUND(AVG((DATE '2025-10-31' - e.HireDate) / 365.25), 1) AS AverageTenureYears,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll
    FROM Locations l
    JOIN Employees e ON e.LocationId = l.Id
    GROUP BY l.Id, l.City, l.State
    ORDER BY Headcount DESC, AnnualPayroll DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT l.City AS OfficeCity, l.State,
           COUNT(e.Id) AS Headcount,
           COUNT(DISTINCT e.DepartmentId) AS DepartmentsRepresented,
           SUM(CASE WHEN e.EmploymentType = 'Full-time' THEN 1 ELSE 0 END) AS FullTime,
           SUM(CASE WHEN e.EmploymentType = 'Part-time' THEN 1 ELSE 0 END) AS PartTime,
           SUM(CASE WHEN e.EmploymentType = 'Contract' THEN 1 ELSE 0 END) AS Contractors,
           ROUND(AVG(DATEDIFF('2025-10-31', e.HireDate) / 365.25), 1) AS AverageTenureYears,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll
    FROM Locations l
    JOIN Employees e ON e.LocationId = l.Id
    GROUP BY l.Id, l.City, l.State
    ORDER BY Headcount DESC, AnnualPayroll DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT l.City AS OfficeCity, l.State,
           COUNT(e.Id) AS Headcount,
           COUNT(DISTINCT e.DepartmentId) AS DepartmentsRepresented,
           SUM(CASE WHEN e.EmploymentType = 'Full-time' THEN 1 ELSE 0 END) AS FullTime,
           SUM(CASE WHEN e.EmploymentType = 'Part-time' THEN 1 ELSE 0 END) AS PartTime,
           SUM(CASE WHEN e.EmploymentType = 'Contract' THEN 1 ELSE 0 END) AS Contractors,
           ROUND(AVG(DAYS_BETWEEN(e.HireDate, '2025-10-31') / 365.25), 1) AS AverageTenureYears,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll
    FROM Locations l
    JOIN Employees e ON e.LocationId = l.Id
    GROUP BY l.Id, l.City, l.State
    ORDER BY Headcount DESC, AnnualPayroll DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT l.City AS OfficeCity, l.State,
           COUNT(e.Id) AS Headcount,
           COUNT(DISTINCT e.DepartmentId) AS DepartmentsRepresented,
           SUM(CASE WHEN e.EmploymentType = 'Full-time' THEN 1 ELSE 0 END) AS FullTime,
           SUM(CASE WHEN e.EmploymentType = 'Part-time' THEN 1 ELSE 0 END) AS PartTime,
           SUM(CASE WHEN e.EmploymentType = 'Contract' THEN 1 ELSE 0 END) AS Contractors,
           ROUND(AVG((DATE '2025-10-31' - e.HireDate) / 365.25), 1) AS AverageTenureYears,
           ROUND(SUM(e.Salary), 2) AS AnnualPayroll
    FROM Locations l
    JOIN Employees e ON e.LocationId = l.Id
    GROUP BY l.Id, l.City, l.State
    ORDER BY Headcount DESC, AnnualPayroll DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT l.City AS OfficeCity, l.State,

               COUNT(e.Id) AS Headcount,

               COUNT(DISTINCT e.DepartmentId) AS DepartmentsRepresented,

               SUM(CASE WHEN e.EmploymentType = 'Full-time' THEN 1 ELSE 0 END) AS FullTime,

               SUM(CASE WHEN e.EmploymentType = 'Part-time' THEN 1 ELSE 0 END) AS PartTime,

               SUM(CASE WHEN e.EmploymentType = 'Contract' THEN 1 ELSE 0 END) AS Contractors,

               ROUND(AVG((DATE '2025-10-31' - e.HireDate) / 365.25), 1) AS AverageTenureYears,

               ROUND(SUM(e.Salary), 2) AS AnnualPayroll

        FROM MAIN.Locations l

        JOIN MAIN.Employees e ON e.LocationId = l.Id

        GROUP BY l.Id, l.City, l.State

        ORDER BY Headcount DESC, AnnualPayroll DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 4. Leave demand and approval status

Compare requested days and approval outcomes by department, leave type, and
status.

=== "SQLite"

    ```sql
    SELECT d.Name AS Department, lr.LeaveType, lr.Status,
           COUNT(*) AS Requests,
           SUM(lr.Days) AS DaysRequested,
           ROUND(AVG(lr.Days), 1) AS AverageRequestDays,
           COUNT(DISTINCT e.Id) AS EmployeesRequesting
    FROM LeaveRequests lr
    JOIN Employees e ON e.Id = lr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, lr.LeaveType, lr.Status
    ORDER BY DaysRequested DESC, Requests DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) d.Name AS Department, lr.LeaveType, lr.Status,
           COUNT(*) AS Requests,
           SUM(lr.Days) AS DaysRequested,
           ROUND(AVG(lr.Days), 1) AS AverageRequestDays,
           COUNT(DISTINCT e.Id) AS EmployeesRequesting
    FROM LeaveRequests lr
    JOIN Employees e ON e.Id = lr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, lr.LeaveType, lr.Status
    ORDER BY DaysRequested DESC, Requests DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT d.Name AS Department, lr.LeaveType, lr.Status,
           COUNT(*) AS Requests,
           SUM(lr.Days) AS DaysRequested,
           ROUND(AVG(lr.Days), 1) AS AverageRequestDays,
           COUNT(DISTINCT e.Id) AS EmployeesRequesting
    FROM LeaveRequests lr
    JOIN Employees e ON e.Id = lr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, lr.LeaveType, lr.Status
    ORDER BY DaysRequested DESC, Requests DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT d.Name AS Department, lr.LeaveType, lr.Status,
           COUNT(*) AS Requests,
           SUM(lr.Days) AS DaysRequested,
           ROUND(AVG(lr.Days), 1) AS AverageRequestDays,
           COUNT(DISTINCT e.Id) AS EmployeesRequesting
    FROM LeaveRequests lr
    JOIN Employees e ON e.Id = lr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, lr.LeaveType, lr.Status
    ORDER BY DaysRequested DESC, Requests DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT d.Name AS Department, lr.LeaveType, lr.Status,
           COUNT(*) AS Requests,
           SUM(lr.Days) AS DaysRequested,
           ROUND(AVG(lr.Days), 1) AS AverageRequestDays,
           COUNT(DISTINCT e.Id) AS EmployeesRequesting
    FROM LeaveRequests lr
    JOIN Employees e ON e.Id = lr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, lr.LeaveType, lr.Status
    ORDER BY DaysRequested DESC, Requests DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT d.Name AS Department, lr.LeaveType, lr.Status,
           COUNT(*) AS Requests,
           SUM(lr.Days) AS DaysRequested,
           ROUND(AVG(lr.Days), 1) AS AverageRequestDays,
           COUNT(DISTINCT e.Id) AS EmployeesRequesting
    FROM LeaveRequests lr
    JOIN Employees e ON e.Id = lr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, lr.LeaveType, lr.Status
    ORDER BY DaysRequested DESC, Requests DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT d.Name AS Department, lr.LeaveType, lr.Status,
           COUNT(*) AS Requests,
           SUM(lr.Days) AS DaysRequested,
           ROUND(AVG(lr.Days), 1) AS AverageRequestDays,
           COUNT(DISTINCT e.Id) AS EmployeesRequesting
    FROM LeaveRequests lr
    JOIN Employees e ON e.Id = lr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, lr.LeaveType, lr.Status
    ORDER BY DaysRequested DESC, Requests DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT d.Name AS Department, lr.LeaveType, lr.Status,

               COUNT(*) AS Requests,

               SUM(lr.Days) AS DaysRequested,

               ROUND(AVG(lr.Days), 1) AS AverageRequestDays,

               COUNT(DISTINCT e.Id) AS EmployeesRequesting

        FROM HR.LeaveRequests lr

        JOIN MAIN.Employees e ON e.Id = lr.EmployeeId

        JOIN HR.Departments d ON d.Id = e.DepartmentId

        GROUP BY d.Id, d.Name, lr.LeaveType, lr.Status

        ORDER BY DaysRequested DESC, Requests DESC

        FETCH FIRST 10 ROWS ONLY;

    ```

### 5. Performance and bonus trends

Compare annual rating distributions, compensation, and bonus rates by
department and review year.

=== "SQLite"

    ```sql
    SELECT d.Name AS Department, pr.ReviewYear,
           COUNT(*) AS Reviews,
           ROUND(AVG(pr.Rating), 2) AS AverageRating,
           SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) AS HighRatings,
           ROUND(100.0 * SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) / COUNT(*), 2) AS HighRatingPercent,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(pr.BonusAmount), 2) AS TotalBonus
    FROM PerformanceReviews pr
    JOIN Employees e ON e.Id = pr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, pr.ReviewYear
    ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC
    LIMIT 10;
    ```

=== "SQL Server"

    ```sql
    SELECT TOP (10) d.Name AS Department, pr.ReviewYear,
           COUNT(*) AS Reviews,
           ROUND(AVG(pr.Rating), 2) AS AverageRating,
           SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) AS HighRatings,
           ROUND(100.0 * SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) / COUNT(*), 2) AS HighRatingPercent,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(pr.BonusAmount), 2) AS TotalBonus
    FROM PerformanceReviews pr
    JOIN Employees e ON e.Id = pr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, pr.ReviewYear
    ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC;
    ```

=== "PostgreSQL"

    ```sql
    SELECT d.Name AS Department, pr.ReviewYear,
           COUNT(*) AS Reviews,
           ROUND(AVG(pr.Rating), 2) AS AverageRating,
           SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) AS HighRatings,
           ROUND(100.0 * SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) / COUNT(*), 2) AS HighRatingPercent,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(pr.BonusAmount), 2) AS TotalBonus
    FROM PerformanceReviews pr
    JOIN Employees e ON e.Id = pr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, pr.ReviewYear
    ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC
    LIMIT 10;
    ```
=== "CockroachDB"

    ```sql
    SELECT d.Name AS Department, pr.ReviewYear,
           COUNT(*) AS Reviews,
           ROUND(AVG(pr.Rating), 2) AS AverageRating,
           SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) AS HighRatings,
           ROUND(100.0 * SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) / COUNT(*), 2) AS HighRatingPercent,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(pr.BonusAmount), 2) AS TotalBonus
    FROM PerformanceReviews pr
    JOIN Employees e ON e.Id = pr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, pr.ReviewYear
    ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC
    LIMIT 10;
    ```

=== "MariaDB"

    ```sql
    SELECT d.Name AS Department, pr.ReviewYear,
           COUNT(*) AS Reviews,
           ROUND(AVG(pr.Rating), 2) AS AverageRating,
           SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) AS HighRatings,
           ROUND(100.0 * SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) / COUNT(*), 2) AS HighRatingPercent,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(pr.BonusAmount), 2) AS TotalBonus
    FROM PerformanceReviews pr
    JOIN Employees e ON e.Id = pr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, pr.ReviewYear
    ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC
    LIMIT 10;
    ```

=== "SAP HANA"

    ```sql
    SELECT d.Name AS Department, pr.ReviewYear,
           COUNT(*) AS Reviews,
           ROUND(AVG(pr.Rating), 2) AS AverageRating,
           SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) AS HighRatings,
           ROUND(100.0 * SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) / COUNT(*), 2) AS HighRatingPercent,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(pr.BonusAmount), 2) AS TotalBonus
    FROM PerformanceReviews pr
    JOIN Employees e ON e.Id = pr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, pr.ReviewYear
    ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC
    LIMIT 10;
    ```

=== "Firebird"

    ```sql
    SELECT d.Name AS Department, pr.ReviewYear,
           COUNT(*) AS Reviews,
           ROUND(AVG(pr.Rating), 2) AS AverageRating,
           SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) AS HighRatings,
           ROUND(100.0 * SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) / COUNT(*), 2) AS HighRatingPercent,
           ROUND(AVG(e.Salary), 2) AS AverageSalary,
           ROUND(SUM(pr.BonusAmount), 2) AS TotalBonus
    FROM PerformanceReviews pr
    JOIN Employees e ON e.Id = pr.EmployeeId
    JOIN Departments d ON d.Id = e.DepartmentId
    GROUP BY d.Id, d.Name, pr.ReviewYear
    ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC
    ROWS 10;
    ```

=== "Db2"



    ```sql

        SELECT d.Name AS Department, pr.ReviewYear,

               COUNT(*) AS Reviews,

               ROUND(AVG(pr.Rating), 2) AS AverageRating,

               SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) AS HighRatings,

               ROUND(100.0 * SUM(CASE WHEN pr.Rating >= 4 THEN 1 ELSE 0 END) / COUNT(*), 2) AS HighRatingPercent,

               ROUND(AVG(e.Salary), 2) AS AverageSalary,

               ROUND(SUM(pr.BonusAmount), 2) AS TotalBonus

        FROM HR.PerformanceReviews pr

        JOIN MAIN.Employees e ON e.Id = pr.EmployeeId

        JOIN HR.Departments d ON d.Id = e.DepartmentId

        GROUP BY d.Id, d.Name, pr.ReviewYear

        ORDER BY pr.ReviewYear DESC, AverageRating DESC, TotalBonus DESC

        FETCH FIRST 10 ROWS ONLY;

    ```
