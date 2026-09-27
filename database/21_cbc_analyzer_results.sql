USE KrishavERPv52;
GO

IF OBJECT_ID('dbo.CbcAnalyzerResults', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CbcAnalyzerResults (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        MessageControlId NVARCHAR(50) NOT NULL DEFAULT '',
        ProcessingId NVARCHAR(5) NOT NULL DEFAULT 'P',
        SampleId NVARCHAR(50) NOT NULL DEFAULT '',
        ResultTypeCode NVARCHAR(20) NOT NULL DEFAULT '',
        ResultTypeName NVARCHAR(100) NOT NULL DEFAULT '',
        PatientIdentifier NVARCHAR(50) NOT NULL DEFAULT '',
        PatientName NVARCHAR(200) NOT NULL DEFAULT '',
        Gender NVARCHAR(20) NULL,
        AgeText NVARCHAR(50) NULL,
        PatientClass NVARCHAR(50) NULL,
        PatientLocation NVARCHAR(200) NULL,
        Tester NVARCHAR(100) NULL,
        Interpreter NVARCHAR(100) NULL,
        RequestedAtUtc DATETIME2 NULL,
        ObservationAtUtc DATETIME2 NULL,
        SpecimenReceivedAtUtc DATETIME2 NULL,
        LoadingMode NVARCHAR(50) NULL,
        BloodMode NVARCHAR(50) NULL,
        TestMode NVARCHAR(50) NULL,
        RefGroup NVARCHAR(50) NULL,
        Remark NVARCHAR(200) NULL,
        RawMessage NVARCHAR(MAX) NOT NULL DEFAULT '',
        ReceivedAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        LabOrderTestId INT NULL
    );

    CREATE INDEX IX_CbcAnalyzerResults_ReceivedAtUtc ON dbo.CbcAnalyzerResults(ReceivedAtUtc);
    CREATE INDEX IX_CbcAnalyzerResults_SampleId ON dbo.CbcAnalyzerResults(SampleId);
END
GO

IF OBJECT_ID('dbo.CbcAnalyzerResultItems', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CbcAnalyzerResultItems (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CbcAnalyzerResultId INT NOT NULL,
        Code NVARCHAR(20) NOT NULL DEFAULT '',
        Name NVARCHAR(200) NOT NULL DEFAULT '',
        Value NVARCHAR(500) NULL,
        Unit NVARCHAR(50) NULL,
        ReferenceRange NVARCHAR(100) NULL,
        AbnormalFlag NVARCHAR(20) NULL,
        SortOrder INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_CbcAnalyzerResultItems_Result FOREIGN KEY (CbcAnalyzerResultId)
            REFERENCES dbo.CbcAnalyzerResults(Id) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('dbo.CbcAnalyzerImages', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CbcAnalyzerImages (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        CbcAnalyzerResultId INT NOT NULL,
        Name NVARCHAR(200) NOT NULL DEFAULT '',
        ImagePath NVARCHAR(300) NOT NULL DEFAULT '',
        SortOrder INT NOT NULL DEFAULT 0,
        CONSTRAINT FK_CbcAnalyzerImages_Result FOREIGN KEY (CbcAnalyzerResultId)
            REFERENCES dbo.CbcAnalyzerResults(Id) ON DELETE CASCADE
    );
END
GO
