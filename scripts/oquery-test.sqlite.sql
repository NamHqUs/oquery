CREATE TABLE [User] (
    [Id] INTEGER PRIMARY KEY,
    [ManagerId] INTEGER,
    [SchoolId] INTEGER,
    [Name] TEXT,
    [Age] INTEGER,
    [Birthday] TEXT,
    [IsMale] INTEGER
);

CREATE TABLE [School] (
    [Id] INTEGER PRIMARY KEY,
    [Name] TEXT,
    [Address] TEXT
);

INSERT INTO [User] ([Id], [ManagerId], [SchoolId], [Name], [Age], [Birthday], [IsMale]) VALUES
    (1, NULL, 1, 'Alice', 45, '1993-01-01', 0),
    (2, 1, 1, 'Bob', 31, '1998-02-02', 1),
    (3, 1, 2, 'Charlie', 28, '1995-03-03', 1),
    (4, 2, 2, 'David', 35, '1988-04-04', 1),
    (5, 4, 3, 'Eve', 22, '2001-05-05', 0),
    (6, 4, 3, 'Frank', 27, '1996-06-06', 1);

INSERT INTO [School] ([Id], [Name], [Address]) VALUES
    (1, 'School A', 'Address A'),
    (2, 'School B', 'Address B'),
    (3, 'School C', 'Address C'),
    (4, 'School D', 'Address D'),
    (5, 'School E', 'Address E');
