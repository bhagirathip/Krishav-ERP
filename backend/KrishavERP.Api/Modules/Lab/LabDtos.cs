namespace KrishavERP.Api.Modules.Lab;

public class LabOrderCreateRequest
{
    public int PatientId { get; set; }
    public int? IpdAdmissionId { get; set; }
    public int RoundOff { get; set; }
    public List<LabOrderLine> Tests { get; set; } = new();
}

public class LabOrderLine
{
    public int LabTestId { get; set; }
    public int? DiscountTypeId { get; set; }
}

public class LabDynamicResultSaveRequest
{
    public string SchemaJson { get; set; } = "";
}

public class LabResultSaveRequest
{
    public List<LabResultValue> Results { get; set; } = new();
}

public class LabResultValue
{
    public int LabResultId { get; set; }
    public string? ResultValue { get; set; }
}
