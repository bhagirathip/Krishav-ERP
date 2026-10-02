namespace KrishavERP.Api.Modules.Lab;

public class LabTest
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Price { get; set; }
    public string? Note { get; set; }
    public string? SchemaJson { get; set; }

    // A separate small reference table (its own columns/rows, filled in once
    // on the master) that prints below the Note on every report for this
    // test - unlike SchemaJson's rows, it isn't patient-result data, so it's
    // read live from the master rather than snapshotted per order-test.
    public string? StaticTableJson { get; set; }

    // Tests sharing the same (non-empty) Group print merged together onto one
    // page when several tests are printed at once; an empty/null Group means
    // this test always prints on its own separate page instead.
    public string? Group { get; set; }
    public bool IsActive { get; set; } = true;
    public List<LabTestComponent> Components { get; set; } = new();
}

public class LabTestComponent
{
    public int Id { get; set; }
    public int LabTestId { get; set; }
    public string Name { get; set; } = "";
    public string? RangeText { get; set; }
    public string? Unit { get; set; }
    public string? DefaultValue { get; set; }
}

public class LabOrder
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = "";
    public int PatientId { get; set; }
    public int? IpdAdmissionId { get; set; }
    public int? BillId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Ordered";
    public List<LabOrderTest> Tests { get; set; } = new();
}

public class LabOrderTest
{
    public int Id { get; set; }
    public int LabOrderId { get; set; }
    public int LabTestId { get; set; }
    public decimal UnitPrice { get; set; }
    public int? DiscountTypeId { get; set; }
    public string? DiscountName { get; set; }
    public string DiscountMode { get; set; } = "Percent";
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string Status { get; set; } = "Pending";
    public string? ResultSchemaJson { get; set; }
    public DateTime? ResultUpdatedAtUtc { get; set; }
    public List<LabResult> Results { get; set; } = new();
}

public class LabResult
{
    public int Id { get; set; }
    public int LabOrderTestId { get; set; }
    public int LabTestComponentId { get; set; }
    public string ComponentName { get; set; } = "";
    public string? ResultValue { get; set; }
    public string? RangeText { get; set; }
    public string? Unit { get; set; }
}

// One row per OBR (test panel) received from a CBC analyzer over the BM500
// LIS protocol - a single HL7 message can carry more than one OBR (e.g. an
// automated count plus a manual/microscopic count), so these are split out
// rather than one row per message. Separate from the existing LabOrder/
// LabOrderTest/LabResult tables used by the manually-entered Lab module -
// this is analyzer-pushed data, kept independent so it can't affect them.
public class CbcAnalyzerResult
{
    public int Id { get; set; }
    public string MessageControlId { get; set; } = "";

    // "P" (patient sample) or "Q" (QC), from MSH-11.
    public string ProcessingId { get; set; } = "P";
    public string SampleId { get; set; } = "";
    public string ResultTypeCode { get; set; } = "";
    public string ResultTypeName { get; set; } = "";
    public string PatientIdentifier { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string? Gender { get; set; }
    public string? AgeText { get; set; }
    public string? PatientClass { get; set; }
    public string? PatientLocation { get; set; }
    public string? Tester { get; set; }
    public string? Interpreter { get; set; }
    public DateTime? RequestedAtUtc { get; set; }
    public DateTime? ObservationAtUtc { get; set; }
    public DateTime? SpecimenReceivedAtUtc { get; set; }
    public string? LoadingMode { get; set; }
    public string? BloodMode { get; set; }
    public string? TestMode { get; set; }
    public string? RefGroup { get; set; }
    public string? Remark { get; set; }
    public string RawMessage { get; set; } = "";
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    // Reserved for manually linking an analyzer-pushed result to an existing
    // LabOrderTest - not wired up yet, kept nullable so this module can ship
    // without touching the existing Lab order/result flow at all.
    public int? LabOrderTestId { get; set; }

    public List<CbcAnalyzerResultItem> Items { get; set; } = new();
    public List<CbcAnalyzerImage> Images { get; set; } = new();
}

public class CbcAnalyzerResultItem
{
    public int Id { get; set; }
    public int CbcAnalyzerResultId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Value { get; set; }
    public string? Unit { get; set; }
    public string? ReferenceRange { get; set; }
    public string? AbnormalFlag { get; set; }
    public int SortOrder { get; set; }
}

public class CbcAnalyzerImage
{
    public int Id { get; set; }
    public int CbcAnalyzerResultId { get; set; }
    public string Name { get; set; } = "";
    public string ImagePath { get; set; } = "";
    public int SortOrder { get; set; }
}
