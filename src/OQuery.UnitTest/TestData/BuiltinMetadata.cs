using OQuery;

namespace OQuery.Test.TestData;

public class BuiltinMetadata
{
    public static IReadOnlyCollection<Entity> SaleDemoData { get; }  =
    [
        new(
            Name: "Customers",
            DbSchema: "sa",
            Fields:
            [
                new("Id", "Id"), new("Name", "Name"), new("Email", "Email"), new("Phone", "Phone"), new("Address", "Address"),
                new("Segment", "Segment"), new("Region", "Region"), new("CustomerSince", "CustomerSince"),
                new("CreatedOn", "CreatedOn"), new("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("Invoices", "Invoices", RelationType.Collection, [new("Id", "CustomerId")])
            ]),
        new(
            Name: "Products",
            DbSchema: "sa",
            Fields:
            [
                new("Id", "Id"), new("Name", "Name"), new("Description", "Description"), new("Category", "Category"),
                new("UnitPrice", "UnitPrice"), new("CostPrice", "CostPrice"),
                new("StockQuantity", "StockQuantity"), new("ReorderLevel", "ReorderLevel"),
                new("CreatedOn", "CreatedOn"), new("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("InvoiceItems", "InvoiceItems", RelationType.Collection, [new("Id", "ProductId")])
            ]),
        new(
            Name: "Invoices",
            DbSchema: "sa",
            Fields:
            [
                new("Id", "Id"), new("CustomerId", "CustomerId"), new("InvoiceNumber", "InvoiceNumber"),
                new("InvoiceDate", "InvoiceDate"), new("DueDate", "DueDate"), new("Status", "Status"),
                new("SalesChannel", "SalesChannel"), new("PaymentMethod", "PaymentMethod"), new("PaidOn", "PaidOn"),
                new("DiscountAmount", "DiscountAmount"), new("TaxAmount", "TaxAmount"),
                new("TotalAmount", "TotalAmount"), new("CreatedOn", "CreatedOn"),
                new("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("Customer", "Customers", RelationType.Lookup, [new("CustomerId", "Id")]),
                new("Items", "InvoiceItems", RelationType.Collection, [new("Id", "InvoiceId")])
            ]),
        new(
            Name: "InvoiceItems",
            DbSchema: "sa",
            Fields:
            [
                new ("Id", "Id"), new("InvoiceId", "InvoiceId"), new("ProductId", "ProductId"),
                new("Quantity", "Quantity"), new("UnitPrice", "UnitPrice"),
                new("DiscountAmount", "DiscountAmount"), new("LineTotal", "LineTotal"),
                new("CreatedOn", "CreatedOn"), new("UpdatedOn", "UpdatedOn")
            ],
            Relations:
            [
                new("Invoice", "Invoices", RelationType.Lookup, [new("InvoiceId", "Id")]),
                new("Product", "Products", RelationType.Lookup, [new("ProductId", "Id")])
            ])
    ];

    public static IReadOnlyCollection<Entity> NorthStarDemoData { get; } =
    [
            new(
                Name: "Locations",
                DbSchema: "Main",
                Fields:
                [
                    new("Id", "Id"), new("Name", "Name"), new("City", "City"), new("State", "State")
                ],
                Relations:
                [
                    new("Employees", "Employees", RelationType.Collection, [new("Id", "LocationId")]),
                    new("Visits", "Visits", RelationType.Collection, [new("Id", "LocationId")])
                ]),
            new(
                Name: "Users",
                DbSchema: "Main",
                Fields:
                [
                    new("Id", "Id"), new("FirstName", "FirstName"), new("LastName", "LastName"),
                    new("BirthDate", "BirthDate"), new("Gender", "Gender"), new("City", "City"), new("State", "State")
                ],
                Relations:
                [
                    new("Orders", "Orders", RelationType.Collection, [new("Id", "UserId")]),
                    new("PatientVisits", "Visits", RelationType.Collection, [new("Id", "UserId")]),
                    new("MemberPolicies", "Policies", RelationType.Collection, [new("Id", "UserId")])
                ]),
            new(
                Name: "Departments",
                DbSchema: "hr",
                Fields:
                [
                    new("Id", "Id"), new("Name", "Name")
                ],
                Relations:
                [
                    new("Employees", "Employees", RelationType.Collection, [new("Id", "DepartmentId")])
                ]),
            new(
                Name: "Employees",
                DbSchema: "Main",
                Fields:
                [
                    new("Id", "Id"), new("FirstName", "FirstName"), new("LastName", "LastName"),
                    new("DepartmentId", "DepartmentId"), new("LocationId", "LocationId"),
                    new("ManagerId", "ManagerId"), new("JobTitle", "JobTitle"), new("HireDate", "HireDate"),
                    new("Salary", "Salary"), new("EmploymentType", "EmploymentType")
                ],
                Relations:
                [
                    new("Department", "Departments", RelationType.Lookup, [new("DepartmentId", "Id")]),
                    new("Location", "Locations", RelationType.Lookup, [new("LocationId", "Id")]),
                    new("Manager", "Employees", RelationType.Lookup, [new("ManagerId", "Id")]),
                    new("DirectReports", "Employees", RelationType.Collection, [new("Id", "ManagerId")]),
                    new("SalesOrders", "Orders", RelationType.Collection, [new("Id", "SalesRepId")]),
                    new("PhysicianVisits", "Visits", RelationType.Collection, [new("Id", "DoctorId")]),
                    new("AgentPolicies", "Policies", RelationType.Collection, [new("Id", "AgentId")]),
                    new("PerformanceReviews", "PerformanceReviews", RelationType.Collection, [new("Id", "EmployeeId")]),
                    new("LeaveRequests", "LeaveRequests", RelationType.Collection, [new("Id", "EmployeeId")])
                ]),
            new(
                Name: "PerformanceReviews",
                DbSchema: "hr",
                Fields:
                [
                    new("Id", "Id"), new("EmployeeId", "EmployeeId"),
                    new("ReviewYear", "ReviewYear"), new("Rating", "Rating"),
                    new("BonusAmount", "BonusAmount")
                ],
                Relations:
                [
                    new("Employee", "Employees", RelationType.Lookup, [new("EmployeeId", "Id")])
                ]),
            new(
                Name: "LeaveRequests",
                DbSchema: "hr",
                Fields:
                [
                    new("Id", "Id"), new("EmployeeId", "EmployeeId"), new("LeaveType", "LeaveType"),
                    new("StartDate", "StartDate"), new("Days", "Days"), new("Status", "Status")
                ],
                Relations:
                [
                    new("Employee", "Employees", RelationType.Lookup, [new("EmployeeId", "Id")])
                ]),
            // The legacy sales demo already defines a different Products entity.
            new(
                "NorthStarProducts",
                "sa",
                "Products",
                [
                    new("Id", "Id"), new("Name", "Name"), new("Category", "Category"),
                    new("UnitPrice", "UnitPrice"), new("CostPrice", "CostPrice")
                ],
                [
                    new("OrderItems", "OrderItems", RelationType.Collection, [new("Id", "ProductId")])
                ]),
            new(
                Name: "Orders",
                DbSchema: "sa",
                Fields:
                [
                    new("Id", "Id"), new("UserId", "UserId"),
                    new("SalesRepId", "SalesRepId"), new("OrderDate", "OrderDate"),
                    new("Channel", "Channel"), new("Status", "Status")
                ],
                Relations:
                [
                    new("User", "Users", RelationType.Lookup, [new("UserId", "Id")]),
                    new("SalesRep", "Employees", RelationType.Lookup, [new("SalesRepId", "Id")]),
                    new("Items", "OrderItems", RelationType.Collection, [new("Id", "OrderId")])
                ]),
            new(
                Name: "OrderItems",
                DbSchema: "sa",
                Fields:
                [
                    new("Id", "Id"), new("OrderId", "OrderId"),
                    new("ProductId", "ProductId"), new("Quantity", "Quantity"),
                    new("UnitPrice", "UnitPrice"), new("DiscountAmount", "DiscountAmount")
                ],
                Relations:
                [
                    new("Order", "Orders", RelationType.Lookup, [new("OrderId", "Id")]),
                    new("Product", "NorthStarProducts", RelationType.Lookup, [new("ProductId", "Id")])
                ]),
            new(
                Name: "Diagnoses",
                DbSchema: "he",
                Fields:
                [
                    new("Id", "Id"), new("Code", "Code"), new("Name", "Name"), new("Category", "Category"),
                    new("IsChronic", "IsChronic")
                ],
                Relations:
                [
                    new("Visits", "Visits", RelationType.Collection, [new("Id", "DiagnosisId")])
                ]),
            new(
                Name: "Visits",
                DbSchema: "he",
                Fields:
                [
                    new("Id", "Id"), new("UserId", "UserId"),
                    new("DoctorId", "DoctorId"), new("LocationId", "LocationId"),
                    new("VisitDate", "VisitDate"), new("VisitType", "VisitType"), new("DiagnosisId", "DiagnosisId"),
                    new("DurationMinutes", "DurationMinutes"), new("Charge", "Charge"), new("Status", "Status")
                ],
                Relations:
                [
                    new("Patient", "Users", RelationType.Lookup, [new("UserId", "Id")]),
                    new("Doctor", "Employees", RelationType.Lookup, [new("DoctorId", "Id")]),
                    new("Location", "Locations", RelationType.Lookup, [new("LocationId", "Id")]),
                    new("Diagnosis", "Diagnoses", RelationType.Lookup, [new("DiagnosisId", "Id")]),
                    new("LabResults", "LabResults", RelationType.Collection, [new("Id", "VisitId")]),
                    new("Claims", "Claims", RelationType.Collection, [new("Id", "VisitId")])
                ]),
            new(
                Name: "LabResults",
                DbSchema: "he",
                Fields:
                [
                    new("Id", "Id"), new("VisitId", "VisitId"), new("TestName", "TestName"),
                    new("ResultValue", "ResultValue"), new("ReferenceMax", "ReferenceMax"),
                    new("IsAbnormal", "IsAbnormal")
                ],
                Relations:
                [
                    new("Visit", "Visits", RelationType.Lookup, [new("VisitId", "Id")])
                ]),
            new(
                Name: "InsurancePlans",
                DbSchema: "in",
                Fields:
                [
                    new("Id", "Id"), new("Name", "Name"), new("Tier", "Tier"),
                    new("MonthlyPremium", "MonthlyPremium"), new("Deductible", "Deductible"),
                    new("OutOfPocketMax", "OutOfPocketMax"), new("CoinsuranceRate", "CoinsuranceRate")
                ],
                Relations:
                [
                    new("Policies", "Policies", RelationType.Collection, [new("Id", "PlanId")])
                ]),
            new(
                Name: "Policies",
                DbSchema: "in",
                Fields:
                [
                    new("Id", "Id"), new("UserId", "UserId"),
                    new("PlanId", "PlanId"), new("AgentId", "AgentId"),
                    new("StartDate", "StartDate"), new("EndDate", "EndDate"), new("Status", "Status")
                ],
                Relations:
                [
                    new("User", "Users", RelationType.Lookup, [new("UserId", "Id")]),
                    new("Plan", "InsurancePlans", RelationType.Lookup, [new("PlanId", "Id")]),
                    new("Agent", "Employees", RelationType.Lookup, [new("AgentId", "Id")]),
                    new("PremiumPayments", "PremiumPayments", RelationType.Collection, [new("Id", "PolicyId")]),
                    new("Claims", "Claims", RelationType.Collection, [new("Id", "PolicyId")])
                ]),
            new(
                Name: "PremiumPayments",
                DbSchema: "in",
                Fields:
                [
                    new("Id", "Id"), new("PolicyId", "PolicyId"),
                    new("PaymentDate", "PaymentDate"), new("Amount", "Amount"), new("Status", "Status")
                ],
                Relations:
                [
                    new("Policy", "Policies", RelationType.Lookup, [new("PolicyId", "Id")])
                ]),
            new(
                Name: "Claims",
                DbSchema: "in",
                Fields:
                [
                    new("Id", "Id"), new("PolicyId", "PolicyId"),
                    new("VisitId", "VisitId"), new("ClaimDate", "ClaimDate"),
                    new("BilledAmount", "BilledAmount"), new("AllowedAmount", "AllowedAmount"),
                    new("PaidAmount", "PaidAmount"), new("Status", "Status"), new("DenialReason", "DenialReason"),
                    new("ProcessedDate", "ProcessedDate")
                ],
                Relations:
                [
                    new("Policy", "Policies", RelationType.Lookup, [new("PolicyId", "Id")]),
                    new("Visit", "Visits", RelationType.Lookup, [new("VisitId", "Id")])
                ])
    ];

    public static List<Entity> UnitTestData { get; } =
    [
        new(
            Name: "School",
            Fields:
            [
                new("Name", "Name"),
            ],
            Relations: [
                new(
                    Name : "Students",
                    RelatedEntity : "Student",
                    Type : RelationType.Collection,
                    RelatedKeys : [
                        new("Id", "SchoolId")
                    ]
                ),
                new(
                    Name : "Teachers",
                    RelatedEntity : "Teacher",
                    Type : RelationType.Collection,
                    RelatedKeys : [
                        new("Id", "SchoolId")
                    ]
                )
            ]),

        new(
            Name : "User",
            Fields : [
                new("Id", "Id"),
                new("Name", "Name"),
                new("ManagerId", "ManagerId"),
                //new("SchoolId", "SchoolId"),
                new("Age", "Age"),
                new("Birthday", "Birthday"),
                new("IsMale", "IsMale"),
            ],

            Relations : [
                new Relation(
                    Name : "Manager",
                    RelatedEntity : "User",
                    Type : RelationType.Lookup,
                    RelatedKeys : [
                        new("ManagerId", "Id")
                    ]),
                new(
                    Name : "Members",
                    RelatedEntity : "User",
                    Type : RelationType.Collection,
                    RelatedKeys : [
                        new("Id", "ManagerId")
                    ]),
                new(
                    Name : "MySchool",
                    RelatedEntity : "School",
                    Type : RelationType.Lookup,
                    RelatedKeys : [
                        new("SchoolId", "Id")
                    ])
            ]),
    ];
}
