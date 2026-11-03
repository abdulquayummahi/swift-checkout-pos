/* SwiftCheckoutPOS: DELETE the old database and create it empty with the current schema.
   For a database whose data is not needed (for example the old development SwiftCheckoutPOSDb).
   How: SSMS -> connect to the SQL Server instance -> File > Open > this file -> check the name below -> Execute (F5).
   Stop the app / Visual Studio debugging first. Everything in the database is lost; there is no undo.
   The same result: dotnet run --project SwiftCheckoutPOS.UI -- --recreate-database (it saves a backup first). */
USE [master];
GO
IF DB_ID(N'SwiftCheckoutPOSDb') IS NOT NULL
BEGIN
    ALTER DATABASE [SwiftCheckoutPOSDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [SwiftCheckoutPOSDb];
END
GO
CREATE DATABASE [SwiftCheckoutPOSDb];
GO
USE [SwiftCheckoutPOSDb];
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE TABLE [Categories] (
        [CategoryId] int NOT NULL IDENTITY,
        [CategoryName] nvarchar(100) NOT NULL,
        [Description] nvarchar(255) NULL,
        CONSTRAINT [PK__Categori__19093A0B4E938104] PRIMARY KEY ([CategoryId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE TABLE [Customers] (
        [CustomerId] int NOT NULL IDENTITY,
        [PhoneNumber] nvarchar(30) NOT NULL,
        [FullName] nvarchar(150) NOT NULL,
        [Address] nvarchar(500) NULL,
        [LoyaltyPoints] int NOT NULL,
        [RegisteredAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Customers] PRIMARY KEY ([CustomerId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE TABLE [SystemSettings] (
        [SettingKey] nvarchar(50) NOT NULL,
        [SettingValue] nvarchar(255) NOT NULL,
        [Description] nvarchar(255) NULL,
        [LastUpdated] datetime2 NOT NULL,
        CONSTRAINT [PK_SystemSettings] PRIMARY KEY ([SettingKey])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE TABLE [Vendors] (
        [VendorId] int NOT NULL IDENTITY,
        [VendorName] nvarchar(150) NOT NULL,
        [ContactName] nvarchar(100) NULL,
        [Phone] nvarchar(50) NULL,
        [Email] nvarchar(100) NULL,
        CONSTRAINT [PK_Vendors] PRIMARY KEY ([VendorId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
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
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_Categories_CategoryName] ON [Categories] ([CategoryName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_Customers_PhoneNumber] ON [Customers] ([PhoneNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_LedgerEntries_TransactionDate] ON [LedgerEntries] ([TransactionDate]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_Products_CategoryId] ON [Products] ([CategoryId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_Products_IsActive_StockQuantity] ON [Products] ([IsActive], [StockQuantity]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_Products_VendorId] ON [Products] ([VendorId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [UQ__Products__177800D302B1BAD5] ON [Products] ([Barcode]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_SaleItems_ProductId] ON [SaleItems] ([ProductId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_SaleItems_SaleId] ON [SaleItems] ([SaleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_Sales_CustomerId] ON [Sales] ([CustomerId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_Sales_SaleDate_OrderStatus] ON [Sales] ([SaleDate], [OrderStatus]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [UQ__Sales__C08AFDAB959AECE3] ON [Sales] ([ReceiptNumber]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_TillReconciliations_CloseTime] ON [TillReconciliations] ([CloseTime]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE UNIQUE INDEX [UQ__Users__536C85E4F504D648] ON [Users] ([Username]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    CREATE INDEX [IX_Vendors_VendorName] ON [Vendors] ([VendorName]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103183746_InitialBaseline'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103183746_InitialBaseline', N'10.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103184222_CashRoundingAmount'
)
BEGIN
    ALTER TABLE [Sales] ADD [RoundingAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103184222_CashRoundingAmount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103184222_CashRoundingAmount', N'10.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103184833_AuditEvents'
)
BEGIN
    CREATE TABLE [AuditEvents] (
        [AuditEventId] bigint NOT NULL IDENTITY,
        [OccurredAt] datetime2 NOT NULL,
        [Actor] nvarchar(100) NOT NULL,
        [Action] nvarchar(50) NOT NULL,
        [EntityType] nvarchar(50) NOT NULL,
        [EntityKey] nvarchar(100) NOT NULL,
        [Reason] nvarchar(200) NULL,
        [Details] nvarchar(2000) NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([AuditEventId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103184833_AuditEvents'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_EntityType_EntityKey] ON [AuditEvents] ([EntityType], [EntityKey]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103184833_AuditEvents'
)
BEGIN
    CREATE INDEX [IX_AuditEvents_OccurredAt] ON [AuditEvents] ([OccurredAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103184833_AuditEvents'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103184833_AuditEvents', N'10.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185207_HeldReservations'
)
BEGIN
    ALTER TABLE [Sales] ADD [HeldAt] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185207_HeldReservations'
)
BEGIN
    ALTER TABLE [Products] ADD [ReservedQuantity] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185207_HeldReservations'
)
BEGIN
    CREATE INDEX [IX_Sales_OrderStatus_HeldAt] ON [Sales] ([OrderStatus], [HeldAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185207_HeldReservations'
)
BEGIN

    UPDATE p SET p.StockQuantity = p.StockQuantity + h.Qty, p.ReservedQuantity = p.ReservedQuantity + h.Qty
    FROM Products p
    JOIN (SELECT si.ProductId, SUM(si.Quantity) AS Qty
          FROM SaleItems si JOIN Sales s ON s.SaleId = si.SaleId
          WHERE s.OrderStatus = N'Held' GROUP BY si.ProductId) h ON h.ProductId = p.ProductId;
    UPDATE Sales SET HeldAt = SaleDate WHERE OrderStatus = N'Held' AND HeldAt IS NULL;
    UPDATE Sales SET OrderStatus = N'Cancelled' WHERE OrderStatus = N'Resumed';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185207_HeldReservations'
)
BEGIN
    EXEC(N'ALTER TABLE [Products] ADD CONSTRAINT [CK_Products_Reserved] CHECK ([ReservedQuantity] >= 0 AND [ReservedQuantity] <= [StockQuantity])');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185207_HeldReservations'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103185207_HeldReservations', N'10.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    ALTER TABLE [Sales] ADD [ApprovedBy] nvarchar(100) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    ALTER TABLE [Sales] ADD [OperationKey] nvarchar(50) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    ALTER TABLE [SaleItems] ADD [DiscountAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    ALTER TABLE [Customers] ADD [DueBalance] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    CREATE TABLE [CustomerDuePayments] (
        [CustomerDuePaymentId] int NOT NULL IDENTITY,
        [CustomerId] int NOT NULL,
        [ReceivedAt] datetime2 NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Tender] nvarchar(20) NOT NULL,
        [Reference] nvarchar(50) NULL,
        [Actor] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CustomerDuePayments] PRIMARY KEY ([CustomerDuePaymentId]),
        CONSTRAINT [FK_CustomerDuePayments_Customers_CustomerId] FOREIGN KEY ([CustomerId]) REFERENCES [Customers] ([CustomerId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    CREATE TABLE [ReceiptSequences] (
        [BusinessDate] date NOT NULL,
        [LastNumber] int NOT NULL,
        CONSTRAINT [PK_ReceiptSequences] PRIMARY KEY ([BusinessDate])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    CREATE TABLE [SalePayments] (
        [SalePaymentId] int NOT NULL IDENTITY,
        [SaleId] int NOT NULL,
        [Tender] nvarchar(20) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Tendered] decimal(18,2) NULL,
        [Reference] nvarchar(50) NULL,
        CONSTRAINT [PK_SalePayments] PRIMARY KEY ([SalePaymentId]),
        CONSTRAINT [CK_SalePayments_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_SalePayments_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([SaleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Sales_OperationKey] ON [Sales] ([OperationKey]) WHERE [OperationKey] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    EXEC(N'ALTER TABLE [Customers] ADD CONSTRAINT [CK_Customers_DueBalance] CHECK ([DueBalance] >= 0)');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    CREATE INDEX [IX_CustomerDuePayments_CustomerId_ReceivedAt] ON [CustomerDuePayments] ([CustomerId], [ReceivedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    CREATE INDEX [IX_SalePayments_SaleId] ON [SalePayments] ([SaleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103185842_TendersReceiptsDue'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103185842_TendersReceiptsDue', N'10.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    ALTER TABLE [Sales] ADD [RefundedAmount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    CREATE TABLE [Refunds] (
        [RefundId] int NOT NULL IDENTITY,
        [SaleId] int NOT NULL,
        [Kind] nvarchar(10) NOT NULL,
        [RefundedAt] datetime2 NOT NULL,
        [Actor] nvarchar(100) NOT NULL,
        [Reason] nvarchar(200) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [VatReversed] decimal(18,2) NOT NULL,
        [DueCancelled] decimal(18,2) NOT NULL,
        [CostReversed] decimal(18,2) NOT NULL,
        [PayoutRounding] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([RefundId]),
        CONSTRAINT [FK_Refunds_Sales_SaleId] FOREIGN KEY ([SaleId]) REFERENCES [Sales] ([SaleId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    CREATE TABLE [RefundItems] (
        [RefundItemId] int NOT NULL IDENTITY,
        [RefundId] int NOT NULL,
        [SaleItemId] int NOT NULL,
        [ProductId] int NOT NULL,
        [Quantity] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CostAtSale] decimal(18,2) NOT NULL,
        [Disposition] nvarchar(10) NOT NULL,
        CONSTRAINT [PK_RefundItems] PRIMARY KEY ([RefundItemId]),
        CONSTRAINT [CK_RefundItems_Quantity] CHECK ([Quantity] > 0),
        CONSTRAINT [FK_RefundItems_Refunds_RefundId] FOREIGN KEY ([RefundId]) REFERENCES [Refunds] ([RefundId]) ON DELETE NO ACTION,
        CONSTRAINT [FK_RefundItems_SaleItems_SaleItemId] FOREIGN KEY ([SaleItemId]) REFERENCES [SaleItems] ([SaleItemId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    CREATE TABLE [RefundPayouts] (
        [RefundPayoutId] int NOT NULL IDENTITY,
        [RefundId] int NOT NULL,
        [Tender] nvarchar(20) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        CONSTRAINT [PK_RefundPayouts] PRIMARY KEY ([RefundPayoutId]),
        CONSTRAINT [FK_RefundPayouts_Refunds_RefundId] FOREIGN KEY ([RefundId]) REFERENCES [Refunds] ([RefundId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    CREATE INDEX [IX_RefundItems_RefundId] ON [RefundItems] ([RefundId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    CREATE INDEX [IX_RefundItems_SaleItemId] ON [RefundItems] ([SaleItemId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    CREATE INDEX [IX_RefundPayouts_RefundId] ON [RefundPayouts] ([RefundId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    CREATE INDEX [IX_Refunds_RefundedAt] ON [Refunds] ([RefundedAt]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    CREATE INDEX [IX_Refunds_SaleId] ON [Refunds] ([SaleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103190656_RefundEvents'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103190656_RefundEvents', N'10.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    ALTER TABLE [Sales] ADD [ShiftId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    ALTER TABLE [Refunds] ADD [ShiftId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    ALTER TABLE [CustomerDuePayments] ADD [ShiftId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    CREATE TABLE [Shifts] (
        [ShiftId] int NOT NULL IDENTITY,
        [Status] nvarchar(10) NOT NULL,
        [OpenedAt] datetime2 NOT NULL,
        [OpenedBy] nvarchar(100) NOT NULL,
        [OpeningFloat] decimal(18,2) NOT NULL,
        [ClosedAt] datetime2 NULL,
        [ClosedBy] nvarchar(100) NULL,
        [CountedCash] decimal(18,2) NULL,
        [ExpectedCash] decimal(18,2) NULL,
        [Difference] decimal(18,2) NULL,
        [DifferenceApprovedBy] nvarchar(100) NULL,
        [Notes] nvarchar(200) NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([ShiftId])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    CREATE TABLE [CashMovements] (
        [CashMovementId] int NOT NULL IDENTITY,
        [ShiftId] int NOT NULL,
        [At] datetime2 NOT NULL,
        [Type] nvarchar(10) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(200) NOT NULL,
        [Actor] nvarchar(100) NOT NULL,
        CONSTRAINT [PK_CashMovements] PRIMARY KEY ([CashMovementId]),
        CONSTRAINT [FK_CashMovements_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([ShiftId]) ON DELETE NO ACTION
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    CREATE INDEX [IX_Sales_ShiftId] ON [Sales] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    CREATE INDEX [IX_Refunds_ShiftId] ON [Refunds] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    CREATE INDEX [IX_CustomerDuePayments_ShiftId] ON [CustomerDuePayments] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    CREATE INDEX [IX_CashMovements_ShiftId] ON [CashMovements] ([ShiftId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_Shifts_OneOpen] ON [Shifts] ([Status]) WHERE [Status] = N''Open''');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    ALTER TABLE [CustomerDuePayments] ADD CONSTRAINT [FK_CustomerDuePayments_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([ShiftId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    ALTER TABLE [Refunds] ADD CONSTRAINT [FK_Refunds_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([ShiftId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    ALTER TABLE [Sales] ADD CONSTRAINT [FK_Sales_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([ShiftId]) ON DELETE NO ACTION;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103191021_Shifts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103191021_Shifts', N'10.0.14');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103192122_UserSecurityStamp'
)
BEGIN
    ALTER TABLE [Users] ADD [SecurityStamp] nvarchar(40) NOT NULL DEFAULT (CONVERT(nvarchar(40), NEWID()));
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261103192122_UserSecurityStamp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261103192122_UserSecurityStamp', N'10.0.14');
END;
GO

COMMIT;
GO


GO
PRINT N'SwiftCheckoutPOSDb recreated with the current schema. Start the app: the first page asks for the first Admin.';
GO
