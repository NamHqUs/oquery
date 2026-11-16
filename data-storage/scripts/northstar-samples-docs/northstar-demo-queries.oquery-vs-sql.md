# NorthStar demo queries: OQuery vs SQL Server

This document rewrites the 20 SQL Server reports in
[northstar-demo-queries.sqlserver.md](./northstar-demo-queries.sqlserver.md)
as OQuery, using the semantic style: root at the business grain, aggregates over
metadata relations, and no helper contexts. The rewrites are based only on the
domain metadata in `data-storage/UserData/123/domains/` and the OQuery syntax
docs. They have **not** been run through the engine yet.

## How to read each section

- **OQuery** is the proposed business form.
- **Why it is easier** lists what the SQL did that OQuery no longer needs.
- **Gaps and checks** lists what the current engine or metadata cannot express,
  and what to verify first.

## Summary

| Report | OQuery status | Main gap or check |
|---|---|---|
| Sales 1 Category and channel profitability | Expressible | None |
| Sales 2 Customer lifetime value | Partial | `LastOrderDate` (Max over Datetime), `ChannelsUsed` (distinct count over String) |
| Sales 3 Sales rep performance | Partial | `Department` and `HomeCity` need Seller relations that the Sales domain does not define |
| Sales 4 Best-selling products | Expressible | Verify relation paths inside `SumIf` predicates |
| Sales 5 Monthly trend | Partial | `Lag` and `WindowSum` over an aggregate alias are not verified |
| Healthcare 1 Diagnosis burden | Expressible | Verify a root-level `Sum` over a nested collection |
| Healthcare 2 Physician scorecard | Expressible | Verify a nested aggregate inside `SumIf` |
| Healthcare 3 High-risk patients | Partial | `LastVisitDate` (Max over Datetime) |
| Healthcare 4 Lab abnormality | Expressible | Verify mixed row and aggregate `Where` |
| Healthcare 5 Visit utilization by month | Expressible | The source SQL is invalid (see below) |
| Insurance 1 Denial analysis | Expressible | None |
| Insurance 2 Plan MLR | Expressible | None |
| Insurance 3 Agent portfolio | Expressible | None |
| Insurance 4 Claims adjudication | Expressible | Simpler at claim grain |
| Insurance 5 Member utilization | Expressible | Uses `Any(Policies)` |
| HR 1 Department compensation | Expressible | Verbose average across reviews |
| HR 2 Manager span | Expressible | Average rating needs a sum and count pair |
| HR 3 Workforce by location | Expressible | `ElapsedYears` divides by 365.2425, SQL uses 365.25 |
| HR 4 Leave demand | Expressible | None |
| HR 5 Performance trends | Expressible | None |

## Sales

### Sales 1. Category and channel profitability

**OQuery**

```text
OrderItems: Product.Category Category,
    Order.Channel Channel,
    Count(distinct Order.Id) OrdersContainingCategory,
    Sum(Quantity) UnitsSold,
    Sum(Quantity * UnitPrice - DiscountAmount).Round(<2>) NetRevenue,
    Sum(DiscountAmount).Round(<2>) Discounts,
    Sum(Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount).Round(<2>) GrossProfit,
    (Sum(Quantity * UnitPrice - DiscountAmount) / NullIf(Count(distinct Order.Id), <0>)).Round(<2>) AverageCategoryRevenuePerOrder
  Where Order.Status = <Completed>
  Order GrossProfit DESC
  Take 10
```

**Why it is easier**
- The per-order CTE disappears. The root is `OrderItems`, so the grain is the line item and the report groups it directly.
- `AverageCategoryRevenuePerOrder` is written as revenue divided by distinct orders. That is the same as the SQL average of per-order category revenue, and it reads as the business definition.

**Gaps and checks**
- `Product.CostPrice` is read through the item's product. This should resolve correctly; verify it.

### Sales 2. Customer lifetime value and recency

**OQuery**

```text
Customers: Id UserId,
    Concat(FirstName, < >, LastName) Customer,
    Orders.CountIf(Status = <Completed>) OrderCount,
    Orders.SumIf(Items.Sum(Quantity * UnitPrice - DiscountAmount), Status = <Completed>).Round(<2>) LifetimeValue
  Where OrderCount > <1>
  Order LifetimeValue DESC
  Take 10
```

**Why it is easier**
- Customer value is a relation aggregate from the customer. There is no join between orders and items to manage.
- The order-status filter is a predicate inside `SumIf`, so the query does not repeat `WHERE` logic.

**Gaps and checks**
- `LastOrderDate` needs `MAX(OrderDate)`. `MaxIf` is typed as Number, so a Datetime maximum is not supported yet. It is omitted.
- `ChannelsUsed` uses `Count(distinct Channel)` to count unique channel values.
- `Orders.SumIf(Items.Sum(...), ...)` uses a nested aggregate as the first argument of `SumIf`. Verify this.
- `Concat` with a single-space literal `< >` is unverified.

### Sales 3. Sales representative performance

**OQuery**

```text
Seller: Id SalesRepId,
    Concat(FirstName, < >, LastName) SalesRep,
    Orders.CountIf(Status = <Completed>) CompletedOrders,
    Orders.SumIf(Items.Sum(Quantity), Status = <Completed>) UnitsSold,
    Orders.SumIf(Items.Sum(Quantity * UnitPrice - DiscountAmount), Status = <Completed>).Round(<2>) NetSales,
    Orders.SumIf(Items.Sum(Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount), Status = <Completed>).Round(<2>) GrossProfit
  Where CompletedOrders > <0>
  Order NetSales DESC
  Take 10
```

**Why it is easier**
- The representative is the root, so there are no joins to the order and item tables.

**Gaps and checks**
- The SQL filters `Department = 'Retail Sales'` and reports `HomeCity`. The Sales domain has no `Department` or `Location` relation on `Seller`; `Seller` only has the raw `DepartmentId` and `LocationId` fields. This needs those relations added to the metadata before the filter and columns can be expressed. Both are omitted here.

### Sales 4. Best-selling products and margin

**OQuery**

```text
Products: Id ProductId,
    Name Product,
    Category,
    Items.SumIf(Quantity, Order.Status = <Completed>) UnitsSold,
    Items.CountIf(distinct Order.Id, Order.Status = <Completed>) OrderCount,
    Items.SumIf(Quantity * UnitPrice - DiscountAmount, Order.Status = <Completed>).Round(<2>) NetRevenue,
    Items.SumIf(Quantity * (UnitPrice - Product.CostPrice) - DiscountAmount, Order.Status = <Completed>).Round(<2>) GrossProfit,
    (100.0 * GrossProfit / NullIf(NetRevenue, <0>)).Round(<2>) MarginPercent
  Where UnitsSold > <0>
  Order GrossProfit DESC
  Take 10
```

**Why it is easier**
- The product is the root, and the sales lines hang off it through `Items`. The join from order items to products is implicit.
- `MarginPercent` reads from the aliases above it, so the margin formula matches the business definition.

**Gaps and checks**
- The predicate `Order.Status = <Completed>` inside `SumIf` uses a relation path. Verify it.
- `Product.CostPrice` inside `Items` refers back to the root product. Verify it resolves to the product's cost.

### Sales 5. Monthly sales trend and momentum

**OQuery**

```text
OrderItems:
    MonthKey(Order.OrderDate) SalesMonth,
    Sum(Quantity * UnitPrice - DiscountAmount).Round(<2>) Revenue,
    Count(distinct Order.Id) OrderCount,
    Lag(Revenue, <1>, SalesMonth).Round(<2>) PriorMonthRevenue,
    (Revenue - PriorMonthRevenue).Round(<2>) RevenueChange,
    WindowSum(Revenue, SalesMonth).Round(<2>) RunningRevenue
  Where Order.Status = <Completed>
  Order SalesMonth DESC
  Take 10
```

**Why it is easier**
- The month is a grouping key, not a CTE. The window columns read as the business measures: prior month, change, and running total.

**Gaps and checks**
- `Lag` and `WindowSum` over an aggregate alias are not verified. The syntax guide marks them as unverified; confirm them before relying on this query.

## Healthcare

### Healthcare 1. Diagnosis burden by clinic

**OQuery**

```text
Visits: Diagnose.Name Diagnosis,
    Diagnose.Category DiagnosisCategory,
    Location.City ClinicCity,
    Count(distinct Id) VisitCount,
    Count(distinct Patient.Id) UniquePatients,
    Avg(Charge).Round(<2>) AverageCharge,
    (100.0 * Sum(LabResults.CountIf(IsAbnormal = <True>)) / NullIf(Sum(LabResults.Count()), <0>)).Round(<2>) AbnormalLabPercent
  Where Status = <Completed>
  Order VisitCount DESC, AbnormalLabPercent DESC
  Take 10
```

**Why it is easier**
- The `visit_labs` CTE is gone. Lab counts are read from each visit's `LabResults` relation.
- Visit, patient, and charge measures all come from the visit root.

**Gaps and checks**
- `Sum(LabResults.CountIf(...))` is a root-level sum over a nested collection aggregate. The syntax guide shows this pattern only from a `Patients` root, so verify it here.

### Healthcare 2. Physician workload and care-quality scorecard

**OQuery**

```text
Doctors: Id DoctorId,
    Concat(FirstName, < >, LastName) Physician,
    Department.Name Department,
    Location.City ClinicCity,
    Examinations.CountIf(Status = <Completed>) VisitCount,
    Examinations.CountIf(distinct Patient.Id, Status = <Completed>) PatientCount,
    Examinations.AvgIf(DurationMinutes, Status = <Completed>).Round(<1>) AverageVisitMinutes,
    Examinations.SumIf(Charge, Status = <Completed>).Round(<2>) Charges,
    (100.0 * Examinations.SumIf(LabResults.CountIf(IsAbnormal = <True>), Status = <Completed>)
        / NullIf(Examinations.SumIf(LabResults.Count(), Status = <Completed>), <0>)).Round(<2>) AbnormalLabPercent
  Where Department.Name In (<Primary Care>, <Specialty Care>, <Emergency Medicine>)
  Order VisitCount DESC, Charges DESC
  Take 10
```

**Why it is easier**
- Doctors stay in the root even with no completed visits, which matches the SQL `LEFT JOIN`. No `LEFT JOIN` needs to be written.
- Each measure reads as a statement about the doctor's examinations.

**Gaps and checks**
- `SumIf` takes a nested aggregate as its first argument. Verify this.
- `Department.Name In (<...>)` with multi-word literals is unverified.

### Healthcare 3. High-risk patient care profile

**OQuery**

```text
Patients: Id PatientId,
    Concat(FirstName, < >, LastName) Patient,
    BirthDate.AgeOn(<2025-10-31>) Age,
    Visits.CountIf(Status = <Completed> AND Diagnose.IsChronic = <True>) ChronicVisits,
    Visits.CountIf(Status = <Completed>) CompletedVisits,
    Visits.SumIf(LabResults.CountIf(IsAbnormal = <True>), Status = <Completed>) AbnormalResults,
    Visits.SumIf(Charge, Status = <Completed>).Round(<2>) TotalCharges
  Where ChronicVisits > <0> AND AbnormalResults > <0>
  Order AbnormalResults DESC, ChronicVisits DESC, TotalCharges DESC
  Take 10
```

**Why it is easier**
- The `HAVING` conditions become `Where` on the aliases, so the patient filter reads as the business rule.
- The diagnosis join and the lab CTE are not written; the relations carry them.

**Gaps and checks**
- `LastVisitDate` needs `MAX(VisitDate)`. `MaxIf` is typed as Number, so it is omitted.
- `SumIf` with a nested aggregate argument needs verifying, as in Healthcare 2.

### Healthcare 4. Lab abnormality patterns

**OQuery**

```text
LabResults: TestName,
    Visit.Diagnose.Category DiagnosisCategory,
    Count() TestCount,
    CountIf(IsAbnormal = <True>) AbnormalCount,
    (100.0 * CountIf(IsAbnormal = <True>) / Count()).Round(<2>) AbnormalPercent,
    Avg(ResultValue).Round(<2>) AverageResult,
    Avg(ReferenceMax).Round(<2>) AverageReferenceMaximum
  Where Visit.Status = <Completed> AND TestCount >= <5>
  Order AbnormalPercent DESC, TestCount DESC
  Take 10
```

**Why it is easier**
- The report is about lab results, so the lab rows are the root. The visit and diagnosis are read through `Visit.Diagnose`.
- The `HAVING COUNT(*) >= 5` becomes a plain condition on the alias.

**Gaps and checks**
- A single `Where` mixes a row filter with an aggregate filter. The syntax guide says aggregate predicates are routed to `HAVING`; verify the mixed form.

### Healthcare 5. Visit-type utilization by month

**OQuery**

```text
Visits: MonthKey(VisitDate) VisitMonth,
    VisitType,
    Count() ScheduledVisits,
    CountIf(Status = <No-Show>) NoShows,
    (100.0 * CountIf(Status = <No-Show>) / Count()).Round(<2>) NoShowPercent,
    AvgIf(DurationMinutes, Status = <Completed>).Round(<1>) AverageMinutes,
    Sum(Charge).Round(<2>) Charges
  Order ScheduledVisits DESC
  Take 10
```

**Why it is easier**
- `MonthKey` gives the month as a grouping key, with no `CONVERT` repeated in `GROUP BY`.

**Source SQL problem**
- The SQL Server version groups by the `VisitMonth` alias (`GROUP BY VisitMonth, VisitType`). SQL Server does not allow a select-list alias in `GROUP BY`, so that report fails as written. It needs the full `CONVERT(...)` expression in `GROUP BY`.

## Insurance

### Insurance 1. Denial analysis by plan and reason

**OQuery**

```text
Claims: Policy.InsurancePlan.Tier Tier,
    Policy.InsurancePlan.Name Plan,
    DenialReason,
    Count() DeniedClaims,
    Sum(BilledAmount).Round(<2>) BilledAmount,
    Sum(AllowedAmount).Round(<2>) AllowedAmount,
    Avg(BilledAmount).Round(<2>) AverageBilledAmount
  Where Status = <Denied>
  Order DeniedClaims DESC, BilledAmount DESC
  Take 10
```

**Why it is easier**
- The claim is the root, and the plan is read through `Policy.InsurancePlan`. The SQL `JOIN`s disappear.

### Insurance 2. Plan profitability and medical loss ratio

**OQuery**

```text
InsurancePlans: Name Plan,
    Tier,
    Policies.Count() PolicyCount,
    Policies.Sum(PremiumPayments.SumIf(Amount, Status = <Paid>)).Round(<2>) PremiumRevenue,
    Policies.Sum(Claims.Count()) ClaimCount,
    Policies.Sum(Claims.Sum(PaidAmount)).Round(<2>) ClaimsPaid,
    Policies.Sum(Claims.CountIf(Status = <Denied>)) DeniedClaims,
    (100.0 * ClaimsPaid / NullIf(PremiumRevenue, <0>)).Round(<2>) MedicalLossRatioPercent
  Order MedicalLossRatioPercent DESC
  Take 10
```

**Why it is easier**
- The two CTEs and the `LEFT JOIN`s are replaced by nested relation aggregates from the plan. Plans with no policies stay in the result, as they do in the SQL.
- The loss ratio reads from the aliases above it.

**Gaps and checks**
- `ClaimsPaid` and `PremiumRevenue` are rounded before the ratio. The SQL divides unrounded sums, so results can differ only with more than two decimal places in the sums.

### Insurance 3. Agent portfolio and premium collection

**OQuery**

```text
Agents: Id AgentId,
    Concat(FirstName, < >, LastName) Agent,
    Location.City OfficeCity,
    Policies.Count() PoliciesSold,
    Policies.CountIf(Status = <Active>) ActivePolicies,
    Policies.Sum(PremiumPayments.SumIf(Amount, Status = <Paid>)).Round(<2>) PaidPremiums,
    Policies.Sum(PremiumPayments.CountIf(Status = <Late>)) LatePayments,
    Policies.Sum(PremiumPayments.CountIf(Status = <Missed>)) MissedPayments
  Where Department.Name = <Insurance Agency>
  Order PaidPremiums DESC
  Take 10
```

**Why it is easier**
- The agent is the root, and the policies and installments hang off it. The `payment_totals` CTE is gone.

### Insurance 4. Claims adjudication by plan

**OQuery**

```text
Claims: Policy.InsurancePlan.Name Plan,
    Policy.InsurancePlan.Tier Tier,
    Count() ClaimsReceived,
    CountIf(Status = <Paid>) PaidClaims,
    CountIf(Status = <Denied>) DeniedClaims,
    (100.0 * DeniedClaims / NullIf(ClaimsReceived, <0>)).Round(<2>) DenialPercent,
    (Sum(DateSub(ClaimDate, ProcessedDate)) * 1.0 / NullIf(Count(ProcessedDate), <0>)).Round(<1>) AverageProcessingDays,
    Sum(PaidAmount).Round(<2>) TotalPaid
  Order ClaimsReceived DESC
  Take 10
```

**Why it is easier**
- The claim is the root, so the counts are plain `Count` and `CountIf`, not nested `Policies.Sum(Claims...)` calls.
- `AverageProcessingDays` uses the same numerator and denominator as the SQL, so pending claims stay out of the denominator.

**Gaps and checks**
- `Count(ProcessedDate)` passes a Datetime to a function typed as Number. The engine accepted the same expression in the earlier generated SQL, but the signature should be aligned.
- The `* 1.0` keeps the average decimal; the source SQL's integer `AVG` truncates. This is a deliberate difference.

### Insurance 5. Member utilization versus premium

**OQuery**

```text
Clients: Id MemberId,
    Concat(FirstName, < >, LastName) Member,
    (IfNull(Policies.Sum(PremiumPayments.SumIf(Amount, Status = <Paid>)), <0>)).Round(<2>) PremiumsCollected,
    (IfNull(Policies.Sum(Claims.Sum(AllowedAmount)), <0>)).Round(<2>) AllowedAmount,
    (IfNull(Policies.Sum(Claims.Sum(PaidAmount)), <0>)).Round(<2>) InsurerPaid,
    IfNull(Policies.Sum(Claims.Count(Id)), <0>) ClaimCount,
    (100.0 * InsurerPaid / NullIf(PremiumsCollected, <0>)).Round(<2>) MemberLossRatioPercent
  Where Any(Policies)
  Order MemberLossRatioPercent DESC, ClaimCount DESC
  Take 10
```

**Why it is easier**
- The member is the root, so the premium and claim totals are separate relation aggregates. The two CTEs and the policy join are not needed.

**Gaps and checks**
- `Where Any(Policies)` is the generic `Any` function and is supported.
- The SQL reports `Plan` and `PolicyStatus` per policy. The member root drops them, because a member can have several policies.

## Human resources

### HR 1. Department compensation and performance

**OQuery**

```text
Departments: Name Department,
    Employees.Count(Id) Headcount,
    Employees.Avg(Salary).Round(<2>) AverageSalary,
    Employees.Sum(Salary).Round(<2>) AnnualPayroll,
    Employees.Avg(PerformanceReviews.AvgIf(Rating, ReviewYear = <2025>)).Round(<2>) AverageRating,
    Employees.Sum(PerformanceReviews.SumIf(BonusAmount, ReviewYear = <2025>)).Round(<2>) TotalBonuses
  Where Headcount > <0>
  Order AnnualPayroll DESC
  Take 10
```

**Why it is easier**
- The department is the root, and employees and their reviews are relation aggregates. The `review_summary` CTE is gone.

**Gaps and checks**
- `AverageRating` is an average of each employee's average, the same as the SQL's `AVG(rs.AverageRating)`. The nested `Avg` over `AvgIf` is unverified.

### HR 2. Manager span and team profile

**OQuery**

```text
Employees: Id ManagerId,
    Concat(FirstName, < >, LastName) Manager,
    Department.Name Department,
    Members.Count(Id) DirectReports,
    Members.Count(distinct LocationId) TeamLocations,
    Members.Avg(Salary).Round(<2>) AverageTeamSalary,
    (Members.Sum(PerformanceReviews.SumIf(Rating, ReviewYear = <2025>)) * 1.0
        / NullIf(Members.Sum(PerformanceReviews.CountIf(ReviewYear = <2025>)), <0>)).Round(<2>) AverageTeamRating,
    Members.Sum(Salary).Round(<2>) TeamPayroll
  Where DirectReports > <0>
  Order DirectReports DESC, TeamPayroll DESC
  Take 10
```

**Why it is easier**
- The manager is the root, and `Members` is the team. The self-join is written as a relation.

**Gaps and checks**
- `AverageTeamRating` is the average of every team review. It needs a sum and count pair because there is no flat average across a nested collection. A future `Members.Avg(PerformanceReviews.Rating)` form would remove this.

### HR 3. Workforce composition by location

**OQuery**

```text
Locations: City OfficeCity,
    State,
    Employees.Count(Id) Headcount,
    Employees.Count(distinct DepartmentId) DepartmentsRepresented,
    Employees.CountIf(EmploymentType = <Full-time>) FullTime,
    Employees.CountIf(EmploymentType = <Part-time>) PartTime,
    Employees.CountIf(EmploymentType = <Contract>) Contractors,
    Employees.Avg(HireDate.ElapsedYears(<2025-10-31>)).Round(<1>) AverageTenureYears,
    Employees.Sum(Salary).Round(<2>) AnnualPayroll
  Where Headcount > <0>
  Order Headcount DESC, AnnualPayroll DESC
  Take 10
```

**Why it is easier**
- The location is the root, and the staff mix is a set of `CountIf` measures. The SQL's `CASE` expressions and the join are not needed.

**Gaps and checks**
- `ElapsedYears` divides by 365.2425, while the SQL divides by 365.25. Results can differ slightly in the last decimal.

### HR 4. Leave demand and approval status

**OQuery**

```text
LeaveRequests: Employee.Department.Name Department,
    LeaveType,
    Status,
    Count() Requests,
    Sum(Days) DaysRequested,
    Avg(Days).Round(<1>) AverageRequestDays,
    Count(distinct EmployeeId) EmployeesRequesting
  Order DaysRequested DESC, Requests DESC
  Take 10
```

**Why it is easier**
- The request is the root, and the department is read through `Employee.Department`. No join is written.

### HR 5. Performance and bonus trends

**OQuery**

```text
PerformanceReviews: Employee.Department.Name Department,
    ReviewYear,
    Count() Reviews,
    Avg(Rating).Round(<2>) AverageRating,
    CountIf(Rating >= <4>) HighRatings,
    (100.0 * CountIf(Rating >= <4>) / Count()).Round(<2>) HighRatingPercent,
    Avg(Employee.Salary).Round(<2>) AverageSalary,
    Sum(BonusAmount).Round(<2>) TotalBonus
  Order ReviewYear DESC, AverageRating DESC, TotalBonus DESC
  Take 10
```

**Why it is easier**
- The review is the root, and the department and salary are read through `Employee`. The percentage reads as `CountIf` divided by `Count`.

## Gaps and open items

1. **Datetime maximum.** `Max` and `MaxIf` are typed as Number, so `LastOrderDate` (Sales 2) and `LastVisitDate` (Healthcare 3) cannot be written. A `Max` over Datetime is needed.
2. **Distinct count over strings.** `Count(distinct Channel)` supports unique string values such as `ChannelsUsed` (Sales 2).
3. **Missing Sales relations.** `Seller` has no relations to `Departments` or `Locations` in the Sales domain. This blocks Sales 3's department filter and home city.
4. **Flat average across nested collections.** The HR 2 team rating needs a sum and count pair. A `Members.Avg(PerformanceReviews.Rating)` form would simplify it.
5. **Window functions over aggregates.** `Lag` and `WindowSum` over an aggregate alias (Sales 5) are unverified.
6. **Nested aggregates as `SumIf` or `CountIf` arguments.** Used in Sales 2, Healthcare 2, Healthcare 3, and Insurance 2. Verify the pattern.
7. **Relation paths inside predicates.** Used in Sales 4, for example `Order.Status = <Completed>` inside `SumIf`. Verify.
8. **Existence conditions in the app.** `Where Any(Policies)` (Insurance 5) uses the generic `Any` function.
9. **Datetime count.** `Count(ProcessedDate)` (Insurance 4) passes a Datetime to a Number-typed function. Align the signature.
10. **Literal forms.** `Concat(FirstName, < >, LastName)` and `Department.Name In (<...>)` with multi-word literals are unverified.
11. **Source SQL problems.** Healthcare 5 groups by a select-list alias, which SQL Server rejects. Insurance 4 and Healthcare 1 use integer `AVG`, which truncates; the OQuery uses `* 1.0` or `Avg` on decimals, so the results differ by design.

## Verification status

- Relation names and field names were checked against the four domain metadata files in `data-storage/UserData/123/domains/`.
- Function names and signatures were checked against [FUNCTION-DEFINITIONS.md](../../../FUNCTION-DEFINITIONS.md) and the syntax guide.
- No OQuery in this file has been executed through the engine. Run each report and record the result before relying on it.
