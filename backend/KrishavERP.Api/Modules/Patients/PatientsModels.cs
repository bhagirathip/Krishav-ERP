namespace KrishavERP.Api.Modules.Patients;

public class PatientSource
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Patient
{
    public int Id { get; set; }
    public string PatientCode { get; set; } = "";
    public string Name { get; set; } = "";
    public int Age { get; set; }
    public string AgeUnit { get; set; } = "Years";
    public string Gender { get; set; } = "";
    public string Phone { get; set; } = "";
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public string MarketingSource { get; set; } = "Walk-in";
    public int? ReferrerId { get; set; }
    public string? Address { get; set; }
}

public class PatientDocument
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public string Category { get; set; } = "Other";
    public string FileName { get; set; } = "";
    public string StoredPath { get; set; } = "";
    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;
}

public class DocumentCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class PatientFollowUp
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public int? OpdVisitId { get; set; }
    public int? ParentFollowUpId { get; set; }
    public DateTime FollowUpDate { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Comment { get; set; }
    public bool FollowUpNeeded { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int? CompletedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
