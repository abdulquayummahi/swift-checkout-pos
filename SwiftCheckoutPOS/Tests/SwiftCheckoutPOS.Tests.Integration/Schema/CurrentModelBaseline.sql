-- BP-10 TEST ONLY: current model, not an upgrade migration.
-- Apply only through the guarded SwiftCheckoutPOS_Test fixture.
CREATE TABLE [Categories] (
    [CategoryId] int NOT NULL IDENTITY,
    [CategoryName] nvarchar(100) NOT NULL,
    [Description] nvarchar(255) NULL,
    CONSTRAINT [PK__Categori__19093A0B4E938104] PRIMARY KEY ([CategoryId])
);
GO


CREATE TABLE [Customers] (
    [CustomerId] int NOT NULL IDENTITY,
    [PhoneNumber] nvarchar(30) NOT NULL,
    [FullName] nvarchar(150) NOT NULL,
    [Address] nvarchar(500) NULL,
    [LoyaltyPoints] int NOT NULL,
    [RegisteredAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId])
);
GO


CREATE TABLE [LedgerEntries] (
    [EntryId] int NOT NULL IDENTITY,
    [TransactionDate] datetime2 NOT NULL,
    [Category] nvarchar(50) NOT NULL,
    [TransactionType] nvarchar(20) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Description] nvarchar(500) NULL,
    [RecordedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_LedgerEntries] PRIMARY KEY ([EntryId])
);
GO


CREATE TABLE [SystemSettings] (
    [SettingKey] nvarchar(50) NOT NULL,
    [SettingValue] nvarchar(255) NOT NULL,
    [Description] nvarchar(255) NULL,
    [LastUpdated] datetime2 NOT NULL,
    CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([SettingKey])
);
GO


CREATE TABLE [TillReconciliations] (
    [ReconciliationId] int NOT NULL IDENTITY,
    [UserId] int NOT NULL,
    [OpenTime] datetime2 NOT NULL,
    [CloseTime] datetime2 NOT NULL,
    [ExpectedCash] decimal(18,2) NOT NULL,
    [CountedCash] decimal(18,2) NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_TillReconciliations] PRIMARY KEY ([ReconciliationId])
);
GO


CREATE TABLE [Users] (
    [UserId] int NOT NULL IDENTITY,
    [Username] nvarchar(50) NOT NULL,
    [FullName] nvarchar(100) NOT NULL,
    [PasswordHash] nvarchar(255) NOT NULL,
    [PasswordSalt] nvarchar(50) NOT NULL,
    [Role] nvarchar(20) NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK__Users__1788CC4C587B2A00] PRIMARY KEY ([UserId])
);
GO


CREATE TABLE [Vendors] (
    [VendorId] int NOT NULL IDENTITY,
    [VendorName] nvarchar(150) NOT NULL,
    [ContactName] nvarchar(100) NULL,
    [Phone] nvarchar(50) NULL,
    [Email] nvarchar(100) NULL,
    CONSTRAINT [PK_Vendors] PRIMARY KEY ([VendorId])
);
GO


CREATE TABLE [Sales] (
    [SaleId] int NOT NULL IDENTITY,
    [ReceiptNumber] nvarchar(50) NOT NULL,
    [SaleDate] datetime NOT NULL DEFAULT ((getdate())),
    [SubTotal] decimal(18,2) NOT NULL,
    [TaxAmount] decimal(18,2) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [PaymentMethod] nvarchar(50) NOT NULL,
    [CustomerId] int NULL,
    [OrderStatus] nvarchar(20) NOT NULL,
    [CashierName] nvarchar(100) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK__Sales__1EE3C3FFEB3A7B6D] PRIMARY KEY ([SaleId]),
    CONSTRAINT [FK_Sales_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId])
);
GO


CREATE TABLE [Products] (
    [ProductId] int NOT NULL IDENTITY,
    [Barcode] nvarchar(50) NOT NULL,
    [ProductName] nvarchar(150) NOT NULL,
    [CategoryId] int NOT NULL,
    [VendorId] int NOT NULL,
    [CostPrice] decimal(18,2) NOT NULL,
    [SellingPrice] decimal(18,2) NOT NULL,
    [TaxRate] decimal(5,2) NOT NULL,
    [UnitOfMeasure] nvarchar(20) NOT NULL,
    [ReorderLevel] int NOT NULL,
    [Description] nvarchar(500) NULL,
    [ImagePath] nvarchar(255) NULL,
    [StockQuantity] int NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK__Products__B40CC6CDBCFF5845] PRIMARY KEY ([ProductId]),
    CONSTRAINT [FK_Products_Vendors_VendorId] FOREIGN KEY ([VendorId]) REFERENCES [Vendors] ([VendorId]) ON DELETE NO ACTION,
    CONSTRAINT [FK__Products__Catego__3C69FB99] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([CategoryId])
);
GO


CREATE TABLE [SaleItems] (
    [SaleItemId] int NOT NULL IDENTITY,
    [SaleId] int NOT NULL,
    [ProductId] int NOT NULL,
    [Quantity] int NOT NULL,
    [UnitPrice] decimal(18,2) NOT NULL,
    [LineTotal] decimal(18,2) NOT NULL,
    [CostAtSale] decimal(18,2) NOT NULL,
    CONSTRAINT [PK__SaleItem__C6059401FE4424A0] PRIMARY KEY ([SaleItemId]),
    CONSTRAINT [FK__SaleItems__Produ__44FF419A] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]),
    CONSTRAINT [FK__SaleItems__SaleI__440B1D61] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([SaleId])
);
GO


CREATE INDEX [IX_Categories_CategoryName] ON [Categories] ([CategoryName]);
GO


CREATE INDEX [IX_Customers_PhoneNumber] ON [Customers] ([PhoneNumber]);
GO


CREATE INDEX [IX_LedgerEntries_TransactionDate] ON [LedgerEntries] ([TransactionDate]);
GO


CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
GO


CREATE INDEX [IX_Products_IsActive_StockQuantity] ON [Products] ([IsActive], [StockQuantity]);
GO


CREATE INDEX [IX_Products_VendorId] ON [Products] ([VendorId]);
GO


CREATE UNIQUE INDEX [UQ__Products__177800D302B1BAD5] ON [Products] ([Barcode]);
GO


CREATE INDEX [IX_SaleItems_ProductId] ON [SaleItems] ([ProductId]);
GO


CREATE INDEX [IX_SaleItems_SaleId] ON [SaleItems] ([SaleId]);
GO


CREATE INDEX [IX_Sales_CustomerId] ON [Sales] ([CustomerId]);
GO


CREATE INDEX [IX_Sales_SaleDate_OrderStatus] ON [Sales] ([SaleDate], [OrderStatus]);
GO


CREATE UNIQUE INDEX [UQ__Sales__C08AFDAB959AECE3] ON [Sales] ([ReceiptNumber]);
GO


CREATE INDEX [IX_TillReconciliations_CloseTime] ON [TillReconciliations] ([CloseTime]);
GO


CREATE UNIQUE INDEX [UQ__Users__536C85E4F504D648] ON [Users] ([Username]);
GO


CREATE INDEX [IX_Vendors_VendorName] ON [Vendors] ([VendorName]);
GO
