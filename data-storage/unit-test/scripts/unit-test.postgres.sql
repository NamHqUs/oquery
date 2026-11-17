CREATE TABLE IF NOT EXISTS public."User" (
    "Id" INTEGER PRIMARY KEY,
    "ManagerId" INTEGER,
    "SchoolId" INTEGER,
    "Name" VARCHAR(255),
    "Age" INTEGER,
    "Birthday" TIMESTAMP,
    "IsMale" BOOLEAN
);

CREATE TABLE IF NOT EXISTS public."School" (
    "Id" INTEGER PRIMARY KEY,
    "Name" VARCHAR(255),
    "Address" VARCHAR(255)
);

INSERT INTO public."User" ("Id", "ManagerId", "SchoolId", "Name", "Age", "Birthday", "IsMale")
VALUES
    (1, NULL, 1, 'Alice', 45, '1993-01-01', FALSE),
    (2, 1, 1, 'Bob', 31, '1998-02-02', TRUE),
    (3, 1, 2, 'Charlie', 28, '1995-03-03', TRUE),
    (4, 2, 2, 'David', 35, '1988-04-04', TRUE),
    (5, 4, 3, 'Eve', 22, '2001-05-05', FALSE),
    (6, 4, 3, 'Frank', 27, '1996-06-06', TRUE)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO public."School" ("Id", "Name", "Address")
VALUES
    (1, 'School A', 'Address A'),
    (2, 'School B', 'Address B'),
    (3, 'School C', 'Address C'),
    (4, 'School D', 'Address D'),
    (5, 'School E', 'Address E')
ON CONFLICT ("Id") DO NOTHING;
