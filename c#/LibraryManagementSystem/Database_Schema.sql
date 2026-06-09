-- 1. Create BookCategories Table
CREATE TABLE "BookCategories" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(100) NOT NULL
);

-- 2. Create Members Table
CREATE TABLE "Members" (
    "Id" SERIAL PRIMARY KEY,
    "Name" VARCHAR(150) NOT NULL,
    "PhoneNumber" VARCHAR(20),
    "Email" VARCHAR(100),
    "Type" INTEGER NOT NULL, -- Enum: 0=Basic, 1=Premium, 2=Student
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE
);

-- 3. Create Books Table (Master Titles)
CREATE TABLE "Books" (
    "Id" SERIAL PRIMARY KEY,
    "Title" VARCHAR(255) NOT NULL,
    "Author" VARCHAR(150) NOT NULL,
    "CategoryId" INTEGER NOT NULL,
    CONSTRAINT "FK_Books_BookCategories" FOREIGN KEY ("CategoryId") REFERENCES "BookCategories" ("Id") ON DELETE CASCADE
);

-- 4. Create BookCopies Table (Physical Inventory)
CREATE TABLE "BookCopies" (
    "Id" SERIAL PRIMARY KEY,
    "BookId" INTEGER NOT NULL,
    "Status" INTEGER NOT NULL, -- Enum: 0=Available, 1=Unavailable, 2=Damaged
    CONSTRAINT "FK_BookCopies_Books" FOREIGN KEY ("BookId") REFERENCES "Books" ("Id") ON DELETE CASCADE
);

-- 5. Create Borrowings Table (The Lending Ledger)
CREATE TABLE "Borrowings" (
    "Id" SERIAL PRIMARY KEY,
    "MemberId" INTEGER NOT NULL,
    "BookCopyId" INTEGER NOT NULL,
    "BookId" INTEGER NOT NULL,
    "BorrowDate" TIMESTAMP NOT NULL,
    "DueDate" TIMESTAMP NOT NULL,
    "ReturnDate" TIMESTAMP NULL,
    "Status" INTEGER NOT NULL, -- Enum: 1=Active, 2=Returned, 3=Overdue
    "FineGenerated" DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT "FK_Borrowings_Members" FOREIGN KEY ("MemberId") REFERENCES "Members" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Borrowings_BookCopies" FOREIGN KEY ("BookCopyId") REFERENCES "BookCopies" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Borrowings_Books" FOREIGN KEY ("BookId") REFERENCES "Books" ("Id") ON DELETE CASCADE,
    CONSTRAINT "CK_Borrowings_Status" CHECK ("Status" IN (1, 2, 3)) -- Our check constraint we fixed earlier!
);

-- 6. Create FinePayments Table (Financial Tracking)
CREATE TABLE "FinePayments" (
    "Id" SERIAL PRIMARY KEY,
    "MemberId" INTEGER NOT NULL,
    "AmountPaid" DECIMAL(18,2) NOT NULL,
    "PaymentDate" TIMESTAMP NOT NULL,
    "IsPaid" BOOLEAN NOT NULL DEFAULT FALSE, -- FALSE = Unpaid Debt, TRUE = Cleared Payment
    "Notes" TEXT NULL,
    CONSTRAINT "FK_FinePayments_Members" FOREIGN KEY ("MemberId") REFERENCES "Members" ("Id") ON DELETE CASCADE
);

--  Insert categories
INSERT INTO "BookCategories" ("Id", "Name") 
VALUES 
(1, 'Fiction'),
(2, 'Technology'),
(3, 'Science'),
(4, 'Biography');