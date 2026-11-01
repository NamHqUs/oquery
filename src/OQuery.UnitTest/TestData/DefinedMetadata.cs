namespace OQuery.Test.TestData;

public class DefinedMetadata
{
    public static readonly List<Entity> Metadata =
    [
        new(
            Name: "School",
            Fields:
            [
                new Field("Name", "Name"),
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
                new Field("Id", "Id"),
                new Field("Name", "Name"),
                //new Field("ManagerId", "ManagerId"),
                //new Field("SchoolId", "SchoolId"),
                new Field("Age", "Age"),
                new Field("Birthday", "Birthday"),
                new Field("IsMale", "IsMale"),
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

        new Entity(
            Name : "Student",
            DbTable : "[User]",
            IsRawSql : true,
            Fields : [
                new Field("Name", "Name"),
                new Field("Age", "Age"),
            ]),
        new Entity(
            Name : "Calculation",
            DbTable : $"(Select t0.*, DATEDIFF(year, t0.BirthDay, GetDate()) As [CalculatedAge], t1.Count " +
                  $"From[User] t0 " +
                    $"Left Join(Select ManagerId, Count(*) Count From [User] Group By ManagerId) t1 On t1.ManagerId = t0.Id)",
            IsRawSql : true,
            Fields : [
                new Field("Id", "Id"),
                new Field("Name", "Name"),
                new Field("CalculatedAge", "CalculatedAge"),
                new Field("Count", "Count"),
            ]),
        new(
            Name : "Teacher",
            Fields : [
                new Field("Id", "Id"),
                new Field("Name", "Name"),
            ],
            Relations : [
                new(
                    Name : "MySchool",
                    RelatedEntity : "School",
                    Type : RelationType.Lookup,
                    RelatedKeys : [
                        new("SchoolId", "Id")
                    ])
            ]
        ),

        new(
                Name: "Customers",
                Fields: [
                    new Field("Id", "Id"),
                    new Field("Name", "Name"),
                    new Field("Email", "Email"),
                    new Field("Phone", "Phone"),
                    new Field("Address", "Address"),
                    new Field("Segment", "Segment"),
                    new Field("Region", "Region"),
                    new Field("CustomerSince", "CustomerSince"),
                    new Field("CreatedOn", "CreatedOn"),
                    new Field("UpdatedOn", "UpdatedOn")
                ],
                Relations: [
                    new(
                        Name: "Invoices",
                        RelatedEntity: "Invoices",
                        Type: RelationType.Collection,
                        RelatedKeys: [new("Id", "CustomerId")])
                ]),
            new(
                Name: "Products",
                Fields: [
                    new Field("Id", "Id"),
                    new Field("Name", "Name"),
                    new Field("Description", "Description"),
                    new Field("Category", "Category"),
                    new Field("UnitPrice", "UnitPrice"),
                    new Field("CostPrice", "CostPrice"),
                    new Field("StockQuantity", "StockQuantity"),
                    new Field("ReorderLevel", "ReorderLevel"),
                    new Field("CreatedOn", "CreatedOn"),
                    new Field("UpdatedOn", "UpdatedOn")
                ],
                Relations: [
                    new(
                        Name: "InvoiceItems",
                        RelatedEntity: "InvoiceItems",
                        Type: RelationType.Collection,
                        RelatedKeys: [new("Id", "ProductId")])
                ]),
            new(
                Name: "Invoices",
                Fields: [
                    new Field("Id", "Id"),
                    new Field("CustomerId", "CustomerId"),
                    new Field("InvoiceNumber", "InvoiceNumber"),
                    new Field("InvoiceDate", "InvoiceDate"),
                    new Field("DueDate", "DueDate"),
                    new Field("Status", "Status"),
                    new Field("SalesChannel", "SalesChannel"),
                    new Field("PaymentMethod", "PaymentMethod"),
                    new Field("PaidOn", "PaidOn"),
                    new Field("DiscountAmount", "DiscountAmount"),
                    new Field("TaxAmount", "TaxAmount"),
                    new Field("TotalAmount", "TotalAmount"),
                    new Field("CreatedOn", "CreatedOn"),
                    new Field("UpdatedOn", "UpdatedOn")
                ],
                Relations: [
                    new(
                        Name: "Customer",
                        RelatedEntity: "Customers",
                        Type: RelationType.Lookup,
                        RelatedKeys: [new("CustomerId", "Id")]),
                    new(
                        Name: "Items",
                        RelatedEntity: "InvoiceItems",
                        Type: RelationType.Collection,
                        RelatedKeys: [new("Id", "InvoiceId")])
                ]),
            new(
                Name: "InvoiceItems",
                Fields: [
                    new Field("Id", "Id"),
                    new Field("InvoiceId", "InvoiceId"),
                    new Field("ProductId", "ProductId"),
                    new Field("Quantity", "Quantity"),
                    new Field("UnitPrice", "UnitPrice"),
                    new Field("DiscountAmount", "DiscountAmount"),
                    new Field("LineTotal", "LineTotal"),
                    new Field("CreatedOn", "CreatedOn"),
                    new Field("UpdatedOn", "UpdatedOn")
                ],
                Relations: [
                    new(
                        Name: "Invoice",
                        RelatedEntity: "Invoices",
                        Type: RelationType.Lookup,
                        RelatedKeys: [new("InvoiceId", "Id")]),
                    new(
                        Name: "Product",
                        RelatedEntity: "Products",
                        Type: RelationType.Lookup,
                        RelatedKeys: [new("ProductId", "Id")])
                ])
    ];
}
