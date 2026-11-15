# OQuery generation notes

This document holds the workflow and lessons for generating OQuery from SQL.
For the language rules, see [OQUERY-SYNTAX.md](./OQUERY-SYNTAX.md). For function
signatures, see [FUNCTION-DEFINITIONS.md](./FUNCTION-DEFINITIONS.md).

## 0. Focus on the business question

When generating an OQuery, start from the business question, not from the
database tables.

1. Name the business entity the question is about, such as `Plan`, `Member`, or
   `Claim`. Write the answer in those terms.
2. Users go straight to that entity. They should not need to reason about which
   table sits underneath it.
3. Any root can give the same result when the query is customized. The root in
   the header is a way to write the query, not a choice that changes the answer.
   When you write the header, pick the entity that matches the business question.
4. Check the totals against the reference SQL before you finish.

### Why the header still matters for totals

The header sets which rows the query reads. A customized query that reads
child rows (such as claims) and sums values from a parent (such as premiums)
repeats each parent value once per child row (fan-out). This is a correctness
trap in the current syntax, so the generated query must avoid it. The examples
below show it.

Rules:

1. Aggregate through relations from the entity the question is about. Do not
   join a child table to it and sum a parent's values, because each row is
   repeated once per child row.
2. Nested aggregates go one level down per nesting. `Policies.Sum(Claims.Sum(PaidAmount))`
   means: per row of the header entity, sum over its policies of each policy's
   claim total.
3. Confirm the totals with one check against the reference SQL before you finish.

### Worked example: paid premium per plan (seed data)

Seed data for plan 8: 49 policies, 214 claims. Each policy's paid premiums add up to
116,290.00 for the plan, counted once per policy.

Correct root, `InsurancePlans`. One row per plan:

```text
InsurancePlans: Name Plan,
    Policies.Sum(PremiumPayments.SumIf(Amount, Status = <Paid>)) PaidPremium
  Where Id = <8>
```

Wrong root, `Claims`. The result has one row per claim, and each claim row also
carries its policy's premiums. Example: policy 56 in the seed data.

- Premium payments for policy 56, all Paid: 510 = 290.00, 511 = 290.00,
  512 = 290.00, 513 = 290.00. Total = 1,160.00.
- Claims for policy 56: claim 85 (paid 94.62) and claim 527 (paid 51.02).

At the correct root, policy 56 contributes 1,160.00 once.
At the `Claims` root, claim 85's row carries the 1,160.00 and claim 527's row
carries it again, so the premium is counted 2 times, 2,320.00.
The claim amounts (94.62 and 51.02) are correct at claim grain; only the premium
is repeated. Across plan 8 the same repetition gives 518,520.00 instead of
116,290.00, about 4.5 times. The fix is not a `DISTINCT`; the fix is to change
the root.

```text
-- Wrong: root at claim grain, premium repeated per claim
Claims: Policy.InsurancePlan.Name Plan,
    ...premium summed from Policy.PremiumPayments...  -- do not do this
```

### Worked example: member versus policy

If one member could have two policies, a `Policies` root repeats the member's
totals on each policy row. A `Clients` root returns one row per member and
aggregates across `Policies`. The seed has one policy per member, so this case
does not show in the seed data; it must still be checked when the metadata or
data changes.

### Checklist (must be filled before returning an OQuery)

- Business entity and what one result row represents. Enforced for the 20 NorthStar reports by
  `ReportRootMatchesDeclaredGrain` in
  [NorthStarDomainSqlServerTests.cs](./src/OQuery.UnitTest/Tests/Providers/NorthStarDomainSqlServerTests.cs).
- For each aggregate: its receiver path and why that level is correct.
- Relation names checked against the metadata. Use the metadata diagram or the
  editor suggestions to confirm each name. A relation has a name on both sides:
  the lookup name on the source and the collection name on the target.
- One total that matches the reference SQL, such as total paid premium.
- Empty-group handling: `IfNull` for totals, `NullIf` for divisors.

## 1. Designing the OQuery from SQL

Start from the business question and the metadata. Use the SQL to confirm the
logic, not as a template for the OQuery structure.

1. Choose the root entity at the grain of the report, and make it the main
   entry header (for example `InsurancePlans:`). Remove physical schema
   prefixes.
2. Prefer one statement. Express related totals as relation aggregates from the
   root, including nested aggregates such as `Policies.Sum(Claims.Sum(PaidAmount))`,
   instead of joining pre-aggregated contexts.
3. Filter inside the aggregate when the condition applies to one metric
   (`SumIf`, `CountIf`). Use `Where` only for conditions on the root rows.
4. Use a context only when one statement cannot express the result cleanly: a
   reusable intermediate result or a grain different from the root. A context
   cannot be the main query (see below).
5. Do not translate CTEs or derived tables one-to-one. A CTE shows the intended
   grain; it does not require a matching context.
6. Use fluent functions (`.Round(<2>)`) and handle NULLs explicitly: `IfNull`
   for totals that may be empty, `NullIf` for divisors.
7. Order by an alias and page with `Take` when result order matters.
8. Repeating an expression is acceptable when it keeps the query to one
   statement. Mention the repetition in the response.
9. Before finalizing, compare the OQuery with the source SQL: row grain, filters,
   NULL handling for empty groups, rounding, and NULL ordering. Report any
   difference, and any assumption you could not verify.

## 2. Lessons learned

### Main query must be an entity header

The main query must start from an entity header such as `OrderItems:`. A
context whose source is another context, such as `SalesTrend is MonthlySales:`,
cannot be the main query. The engine rejects it with
`Entity [MonthlySales] does not exist in domain`. Declare contexts first and
join them into an entity-based main query.

### Window functions over aggregates are not yet verified

Window functions such as `Lag` or `WindowSum` over an aggregate alias in the
same context are not yet verified against the engine. Confirm the behavior
before relying on it.

### Metadata relation names may not match their inverse

The `TargetRelationName` in metadata can disagree with the relation it
describes. For example, the inverse of `Policies.InsurancePlan` on
`InsurancePlans` is listed as `InsurancePlan`. Confirm collection names against
the engine's generated SQL, and state the name you assumed.

### Check generated SQL against the source

When engine output is available, compare it with the source SQL for:

- row grain and join fan-out;
- filters and their placement;
- NULL handling for plans or groups with no rows;
- numeric types: an integer multiplier such as `100` can truncate a division
  that the source computed with `100.0`;
- dialect: generated `LIMIT` or double-quoted identifiers are not SQL Server
  syntax.

### Verify inferred syntax

If a pattern is inferred from a general rule rather than shown in a documented
example, state the assumption before giving the query so it can be checked.
