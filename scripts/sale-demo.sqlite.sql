PRAGMA foreign_keys = ON;

ATTACH DATABASE ':memory:' AS sa;

DROP TABLE IF EXISTS sa.InvoiceItems;
DROP TABLE IF EXISTS sa.Invoices;
DROP TABLE IF EXISTS sa.Products;
DROP TABLE IF EXISTS sa.Customers;

CREATE TABLE sa.Customers (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Email TEXT NOT NULL,
    Phone TEXT NOT NULL,
    Address TEXT NOT NULL,
    Segment TEXT NOT NULL,
    Region TEXT NOT NULL,
    CustomerSince TEXT NOT NULL,
    CreatedOn TEXT NOT NULL,
    UpdatedOn TEXT NOT NULL
);

CREATE TABLE sa.Products (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL,
    Description TEXT NOT NULL,
    Category TEXT NOT NULL,
    UnitPrice NUMERIC NOT NULL,
    CostPrice NUMERIC NOT NULL,
    StockQuantity INTEGER NOT NULL,
    ReorderLevel INTEGER NOT NULL,
    CreatedOn TEXT NOT NULL,
    UpdatedOn TEXT NOT NULL
);

CREATE TABLE sa.Invoices (
    Id INTEGER PRIMARY KEY,
    CustomerId INTEGER NOT NULL,
    InvoiceNumber TEXT NOT NULL UNIQUE,
    InvoiceDate TEXT NOT NULL,
    DueDate TEXT NOT NULL,
    Status TEXT NOT NULL,
    SalesChannel TEXT NOT NULL,
    PaymentMethod TEXT NOT NULL,
    PaidOn TEXT NULL,
    DiscountAmount NUMERIC NOT NULL DEFAULT 0,
    TaxAmount NUMERIC NOT NULL,
    TotalAmount NUMERIC NOT NULL,
    CreatedOn TEXT NOT NULL,
    UpdatedOn TEXT NOT NULL,
    FOREIGN KEY (CustomerId) REFERENCES Customers (Id)
);

CREATE TABLE sa.InvoiceItems (
    Id INTEGER PRIMARY KEY,
    InvoiceId INTEGER NOT NULL,
    ProductId INTEGER NOT NULL,
    Quantity INTEGER NOT NULL,
    UnitPrice NUMERIC NOT NULL,
    DiscountAmount NUMERIC NOT NULL DEFAULT 0,
    LineTotal NUMERIC NOT NULL,
    CreatedOn TEXT NOT NULL,
    UpdatedOn TEXT NOT NULL,
    FOREIGN KEY (InvoiceId) REFERENCES Invoices (Id),
    FOREIGN KEY (ProductId) REFERENCES Products (Id)
);

INSERT INTO sa.Customers
    (Id, Name, Email, Phone, Address, Segment, Region, CustomerSince, CreatedOn, UpdatedOn)
VALUES
    (1, 'Alice Johnson', 'alice.johnson@example.com', '555-0101', '101 Main Street', 'Enterprise', 'Northeast', '2023-04-12', '2025-01-01', '2025-01-01'),
    (2, 'Bob Smith', 'bob.smith@example.com', '555-0102', '202 Oak Avenue', 'Small Business', 'Midwest', '2024-02-18', '2025-01-02', '2025-01-02'),
    (3, 'Carol Williams', 'carol.williams@example.com', '555-0103', '303 Pine Road', 'Enterprise', 'West', '2022-11-05', '2025-01-03', '2025-01-03'),
    (4, 'David Brown', 'david.brown@example.com', '555-0104', '404 Maple Lane', 'Small Business', 'South', '2024-06-21', '2025-01-04', '2025-01-04'),
    (5, 'Emma Davis', 'emma.davis@example.com', '555-0105', '505 Cedar Drive', 'Consumer', 'Northeast', '2025-01-10', '2025-01-05', '2025-01-05'),
    (6, 'Frank Miller', 'frank.miller@example.com', '555-0106', '606 Elm Street', 'Enterprise', 'Midwest', '2023-08-30', '2025-01-06', '2025-01-06'),
    (7, 'Grace Wilson', 'grace.wilson@example.com', '555-0107', '707 Birch Boulevard', 'Consumer', 'West', '2024-12-02', '2025-01-07', '2025-01-07'),
    (8, 'Henry Moore', 'henry.moore@example.com', '555-0108', '808 Willow Way', 'Small Business', 'South', '2024-01-14', '2025-01-08', '2025-01-08'),
    (9, 'Ivy Taylor', 'ivy.taylor@example.com', '555-0109', '909 Spruce Court', 'Enterprise', 'Northeast', '2023-03-27', '2025-01-09', '2025-01-09'),
    (10, 'Jack Anderson', 'jack.anderson@example.com', '555-0110', '1010 Ash Place', 'Consumer', 'Midwest', '2025-02-01', '2025-01-10', '2025-01-10');

INSERT INTO sa.Products
    (Id, Name, Description, Category, UnitPrice, CostPrice, StockQuantity, ReorderLevel, CreatedOn, UpdatedOn)
VALUES
    (1, 'Laptop', '15-inch everyday laptop', 'Computers', 899.00, 650.00, 25, 10, '2025-01-01', '2025-01-01'),
    (2, 'Wireless Mouse', 'Ergonomic wireless mouse', 'Office Accessories', 25.00, 12.00, 100, 25, '2025-01-01', '2025-01-01'),
    (3, 'Keyboard', 'Full-size USB keyboard', 'Office Accessories', 45.00, 22.00, 75, 20, '2025-01-01', '2025-01-01'),
    (4, 'Monitor', '24-inch full HD monitor', 'Displays', 199.00, 125.00, 40, 12, '2025-01-01', '2025-01-01'),
    (5, 'USB Cable', 'Two-meter USB-C cable', 'Office Accessories', 12.00, 4.00, 200, 50, '2025-01-01', '2025-01-01'),
    (6, 'Headset', 'Noise-reducing wired headset', 'Audio Video', 59.00, 31.00, 60, 15, '2025-01-01', '2025-01-01'),
    (7, 'Webcam', '1080p desktop webcam', 'Audio Video', 79.00, 42.00, 35, 10, '2025-01-01', '2025-01-01'),
    (8, 'Desk Lamp', 'Adjustable LED desk lamp', 'Office Accessories', 35.00, 15.00, 50, 15, '2025-01-01', '2025-01-01'),
    (9, 'Notebook', 'Hardcover ruled notebook', 'Office Accessories', 8.00, 2.50, 150, 40, '2025-01-01', '2025-01-01'),
    (10, 'Pen Set', 'Pack of five blue pens', 'Office Accessories', 6.00, 1.50, 300, 75, '2025-01-01', '2025-01-01');

INSERT INTO sa.Invoices
    (Id, CustomerId, InvoiceNumber, InvoiceDate, DueDate, Status, SalesChannel, PaymentMethod, PaidOn, DiscountAmount, TaxAmount, TotalAmount, CreatedOn, UpdatedOn)
VALUES
    (1, 1, 'INV-1001', '2025-02-01', '2025-03-03', 'Paid', 'Online', 'Card', '2025-02-10', 0, 75.92, 949.00, '2025-02-01', '2025-02-10'),
    (2, 1, 'INV-1002', '2025-03-01', '2025-03-31', 'Paid', 'Sales Rep', 'ACH', '2025-03-08', 14.00, 16.88, 211.00, '2025-03-01', '2025-03-08'),
    (3, 2, 'INV-1003', '2025-02-05', '2025-03-07', 'Paid', 'Online', 'Card', '2025-02-12', 0, 8.32, 104.00, '2025-02-05', '2025-02-12'),
    (4, 3, 'INV-1004', '2025-02-10', '2025-03-12', 'Sent', 'Online', 'Card', NULL, 18.00, 85.12, 1064.00, '2025-02-10', '2025-02-10'),
    (5, 3, 'INV-1005', '2025-03-10', '2025-04-09', 'Paid', 'Online', 'Card', '2025-03-18', 0, 8.64, 108.00, '2025-03-10', '2025-03-18'),
    (6, 3, 'INV-1006', '2025-04-10', '2025-05-10', 'Draft', 'Sales Rep', 'Invoice', NULL, 0, 0, 259.00, '2025-04-10', '2025-04-10'),
    (7, 4, 'INV-1007', '2025-02-15', '2025-03-17', 'Paid', 'Store', 'Cash', '2025-02-20', 0, 17.92, 224.00, '2025-02-15', '2025-02-20'),
    (8, 4, 'INV-1008', '2025-03-15', '2025-04-14', 'Sent', 'Online', 'Card', NULL, 0, 8.24, 103.00, '2025-03-15', '2025-03-15'),
    (9, 5, 'INV-1009', '2025-02-20', '2025-03-22', 'Paid', 'Store', 'Card', '2025-02-25', 0, 12.00, 150.00, '2025-02-20', '2025-02-25'),
    (10, 6, 'INV-1010', '2025-02-25', '2025-03-27', 'Paid', 'Sales Rep', 'ACH', '2025-03-01', 30.00, 74.96, 937.00, '2025-02-25', '2025-03-01'),
    (11, 6, 'INV-1011', '2025-03-25', '2025-04-24', 'Sent', 'Sales Rep', 'Invoice', NULL, 0, 9.12, 114.00, '2025-03-25', '2025-03-25'),
    (12, 6, 'INV-1012', '2025-04-25', '2025-05-25', 'Draft', 'Online', 'Invoice', NULL, 0, 0, 248.00, '2025-04-25', '2025-04-25'),
    (13, 7, 'INV-1013', '2025-03-02', '2025-04-01', 'Paid', 'Online', 'Card', '2025-03-10', 0, 10.24, 128.00, '2025-03-02', '2025-03-10'),
    (14, 7, 'INV-1014', '2025-04-02', '2025-05-02', 'Sent', 'Store', 'Card', NULL, 0, 6.32, 79.00, '2025-04-02', '2025-04-02'),
    (15, 8, 'INV-1015', '2025-03-05', '2025-04-04', 'Paid', 'Sales Rep', 'ACH', '2025-03-12', 0, 8.32, 104.00, '2025-03-05', '2025-03-12'),
    (16, 9, 'INV-1016', '2025-03-08', '2025-04-07', 'Paid', 'Online', 'Card', '2025-03-15', 0, 73.92, 924.00, '2025-03-08', '2025-03-15'),
    (17, 9, 'INV-1017', '2025-04-08', '2025-05-08', 'Sent', 'Sales Rep', 'Invoice', NULL, 0, 19.68, 246.00, '2025-04-08', '2025-04-08'),
    (18, 9, 'INV-1018', '2025-05-08', '2025-06-07', 'Draft', 'Online', 'Invoice', NULL, 0, 0, 59.00, '2025-05-08', '2025-05-08'),
    (19, 10, 'INV-1019', '2025-03-12', '2025-04-11', 'Paid', 'Store', 'Cash', '2025-03-20', 0, 19.12, 239.00, '2025-03-12', '2025-03-20'),
    (20, 10, 'INV-1020', '2025-04-12', '2025-05-12', 'Sent', 'Online', 'Card', NULL, 0, 8.32, 104.00, '2025-04-12', '2025-04-12');

INSERT INTO sa.InvoiceItems
    (Id, InvoiceId, ProductId, Quantity, UnitPrice, LineTotal, CreatedOn, UpdatedOn)
VALUES
    (1, 1, 1, 1, 899.00, 899.00, '2025-02-01', '2025-02-01'),
    (2, 1, 2, 2, 25.00, 50.00, '2025-02-01', '2025-02-01'),
    (3, 2, 4, 1, 199.00, 199.00, '2025-03-01', '2025-03-01'),
    (4, 2, 5, 1, 12.00, 12.00, '2025-03-01', '2025-03-01'),
    (5, 3, 3, 1, 45.00, 45.00, '2025-02-05', '2025-02-05'),
    (6, 3, 6, 1, 59.00, 59.00, '2025-02-05', '2025-02-05'),
    (7, 4, 1, 1, 899.00, 899.00, '2025-02-10', '2025-02-10'),
    (8, 4, 7, 1, 79.00, 79.00, '2025-02-10', '2025-02-10'),
    (9, 4, 8, 2, 35.00, 70.00, '2025-02-10', '2025-02-10'),
    (10, 4, 9, 2, 8.00, 16.00, '2025-02-10', '2025-02-10'),
    (11, 5, 6, 1, 59.00, 59.00, '2025-03-10', '2025-03-10'),
    (12, 5, 10, 2, 6.00, 12.00, '2025-03-10', '2025-03-10'),
    (13, 5, 5, 1, 12.00, 12.00, '2025-03-10', '2025-03-10'),
    (14, 5, 2, 1, 25.00, 25.00, '2025-03-10', '2025-03-10'),
    (15, 6, 4, 1, 199.00, 199.00, '2025-04-10', '2025-04-10'),
    (16, 6, 5, 2, 12.00, 24.00, '2025-04-10', '2025-04-10'),
    (17, 6, 10, 6, 6.00, 36.00, '2025-04-10', '2025-04-10'),
    (18, 7, 4, 1, 199.00, 199.00, '2025-02-15', '2025-02-15'),
    (19, 7, 2, 1, 25.00, 25.00, '2025-02-15', '2025-02-15'),
    (20, 8, 3, 1, 45.00, 45.00, '2025-03-15', '2025-03-15'),
    (21, 8, 9, 5, 8.00, 40.00, '2025-03-15', '2025-03-15'),
    (22, 8, 10, 1, 6.00, 6.00, '2025-03-15', '2025-03-15'),
    (23, 8, 5, 1, 12.00, 12.00, '2025-03-15', '2025-03-15'),
    (24, 9, 6, 1, 59.00, 59.00, '2025-02-20', '2025-02-20'),
    (25, 9, 7, 1, 79.00, 79.00, '2025-02-20', '2025-02-20'),
    (26, 9, 5, 1, 12.00, 12.00, '2025-02-20', '2025-02-20'),
    (27, 10, 1, 1, 899.00, 899.00, '2025-02-25', '2025-02-25'),
    (28, 10, 5, 2, 12.00, 24.00, '2025-02-25', '2025-02-25'),
    (29, 10, 10, 1, 6.00, 6.00, '2025-02-25', '2025-02-25'),
    (30, 10, 9, 1, 8.00, 8.00, '2025-02-25', '2025-02-25'),
    (31, 11, 8, 2, 35.00, 70.00, '2025-03-25', '2025-03-25'),
    (32, 11, 10, 4, 6.00, 24.00, '2025-03-25', '2025-03-25'),
    (33, 11, 5, 1, 12.00, 12.00, '2025-03-25', '2025-03-25'),
    (34, 11, 9, 1, 8.00, 8.00, '2025-03-25', '2025-03-25'),
    (35, 12, 4, 1, 199.00, 199.00, '2025-04-25', '2025-04-25'),
    (36, 12, 5, 1, 12.00, 12.00, '2025-04-25', '2025-04-25'),
    (37, 12, 10, 2, 6.00, 12.00, '2025-04-25', '2025-04-25'),
    (38, 12, 2, 1, 25.00, 25.00, '2025-04-25', '2025-04-25'),
    (39, 13, 3, 2, 45.00, 90.00, '2025-03-02', '2025-03-02'),
    (40, 13, 5, 1, 12.00, 12.00, '2025-03-02', '2025-03-02'),
    (41, 13, 10, 3, 6.00, 18.00, '2025-03-02', '2025-03-02'),
    (42, 13, 9, 1, 8.00, 8.00, '2025-03-02', '2025-03-02'),
    (43, 14, 7, 1, 79.00, 79.00, '2025-04-02', '2025-04-02'),
    (44, 15, 3, 1, 45.00, 45.00, '2025-03-05', '2025-03-05'),
    (45, 15, 6, 1, 59.00, 59.00, '2025-03-05', '2025-03-05'),
    (46, 16, 1, 1, 899.00, 899.00, '2025-03-08', '2025-03-08'),
    (47, 16, 2, 1, 25.00, 25.00, '2025-03-08', '2025-03-08'),
    (48, 17, 4, 1, 199.00, 199.00, '2025-04-08', '2025-04-08'),
    (49, 17, 8, 1, 35.00, 35.00, '2025-04-08', '2025-04-08'),
    (50, 17, 5, 1, 12.00, 12.00, '2025-04-08', '2025-04-08'),
    (51, 18, 6, 1, 59.00, 59.00, '2025-05-08', '2025-05-08'),
    (52, 19, 4, 1, 199.00, 199.00, '2025-03-12', '2025-03-12'),
    (53, 19, 9, 2, 8.00, 16.00, '2025-03-12', '2025-03-12'),
    (54, 19, 10, 2, 6.00, 12.00, '2025-03-12', '2025-03-12'),
    (55, 19, 5, 1, 12.00, 12.00, '2025-03-12', '2025-03-12'),
    (56, 20, 7, 1, 79.00, 79.00, '2025-04-12', '2025-04-12'),
    (57, 20, 2, 1, 25.00, 25.00, '2025-04-12', '2025-04-12');

CREATE INDEX IF NOT EXISTS sa.IX_Invoices_InvoiceDate_Status
    ON Invoices (InvoiceDate, Status);
CREATE INDEX IF NOT EXISTS sa.IX_Invoices_CustomerId
    ON Invoices (CustomerId);
CREATE INDEX IF NOT EXISTS sa.IX_InvoiceItems_ProductId
    ON InvoiceItems (ProductId);