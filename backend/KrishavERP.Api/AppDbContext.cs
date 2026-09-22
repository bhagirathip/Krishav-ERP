using Microsoft.EntityFrameworkCore;
namespace KrishavERP.Api;
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options): base(options)
    {
    }
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppRole> AppRoles => Set<AppRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientSource> PatientSources => Set<PatientSource>();
    public DbSet<OpdVisit> OpdVisits => Set<OpdVisit>();
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillTypeMaster> BillTypes => Set<BillTypeMaster>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WardBed> WardBeds => Set<WardBed>();
    public DbSet<IpdAdmission> IpdAdmissions => Set<IpdAdmission>();
    public DbSet<IpdVital> IpdVitals => Set<IpdVital>();
    public DbSet<IpdDoctorNote> IpdDoctorNotes => Set<IpdDoctorNote>();
    public DbSet<IpdNursingNote> IpdNursingNotes => Set<IpdNursingNote>();
    public DbSet<IpdDocument> IpdDocuments => Set<IpdDocument>();
    public DbSet<OtBooking> OtBookings => Set<OtBooking>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<PharmacyBillReservation> PharmacyBillReservations => Set<PharmacyBillReservation>();
    public DbSet<PharmacyDistributor> PharmacyDistributors => Set<PharmacyDistributor>();
    public DbSet<PharmacyMedicineType> PharmacyMedicineTypes => Set<PharmacyMedicineType>();
    public DbSet<PharmacyPurchaseInvoice> PharmacyPurchaseInvoices => Set<PharmacyPurchaseInvoice>();
    public DbSet<PharmacyPurchaseItem> PharmacyPurchaseItems => Set<PharmacyPurchaseItem>();
    public DbSet<PharmacySale> PharmacySales => Set<PharmacySale>();
    public DbSet<PharmacySaleItem> PharmacySaleItems => Set<PharmacySaleItem>();
    public DbSet<PharmacyExpiryAction> PharmacyExpiryActions => Set<PharmacyExpiryAction>();
    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<LabTestComponent> LabTestComponents => Set<LabTestComponent>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<LabOrderTest> LabOrderTests => Set<LabOrderTest>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ReportCategory> ReportCategories => Set<ReportCategory>();
    public DbSet<DiscountType> DiscountTypes => Set<DiscountType>();
    public DbSet<Referrer> Referrers => Set<Referrer>();
    public DbSet<ReferralPayout> ReferralPayouts => Set<ReferralPayout>();
    public DbSet<PatientFollowUp> PatientFollowUps => Set<PatientFollowUp>();
    public DbSet<StaffDesignation> StaffDesignations => Set<StaffDesignation>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<StaffAttendance> StaffAttendances => Set<StaffAttendance>();
    public DbSet<SalaryPayment> SalaryPayments => Set<SalaryPayment>();
    public DbSet<DailyExpense> DailyExpenses => Set<DailyExpense>();
    public DbSet<DoctorSettlement> DoctorSettlements => Set<DoctorSettlement>();
    public DbSet<PharmacyPurchasePayment> PharmacyPurchasePayments => Set<PharmacyPurchasePayment>();
    public DbSet<LabPurchaseExpense> LabPurchaseExpenses => Set<LabPurchaseExpense>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(x=>x.Username).IsUnique();
        b.Entity<AppRole>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<RolePermission>().HasIndex(x=>new{x.RoleId,x.Module}).IsUnique();
        b.Entity<Doctor>().Property(x=>x.ConsultationCharge).HasPrecision(18,2);
        b.Entity<Doctor>().Property(x=>x.IpdCharge).HasPrecision(18,2);
        b.Entity<Patient>().HasIndex(x=>x.PatientCode).IsUnique();
        b.Entity<PatientSource>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<DocumentCategory>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<ReportCategory>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<DiscountType>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<DiscountType>().Property(x=>x.Value).HasPrecision(18,2);
        b.Entity<Referrer>().HasIndex(x=>x.ReferrerCode).IsUnique();
        b.Entity<Referrer>().HasIndex(x=>x.Name);
        b.Entity<ReferralPayout>().HasIndex(x=>new{x.ReferrerId,x.PatientId}).IsUnique();
        b.Entity<ReferralPayout>().Property(x=>x.Amount).HasPrecision(18,2);
        b.Entity<PatientFollowUp>()
            .HasIndex(x => new { x.FollowUpDate, x.Status });
        b.Entity<PatientFollowUp>()
            .HasIndex(x => new { x.PatientId, x.FollowUpDate });
        b.Entity<StaffDesignation>().HasIndex(x => x.Name).IsUnique();
        b.Entity<StaffMember>().HasIndex(x => x.StaffCode).IsUnique();
        b.Entity<StaffMember>().HasIndex(x => x.Name);
        b.Entity<StaffMember>().Property(x => x.MonthlySalary).HasPrecision(18,2);
        b.Entity<StaffAttendance>().HasIndex(x => new { x.StaffId, x.AttendanceDate }).IsUnique();
        b.Entity<SalaryPayment>().HasIndex(x => new { x.StaffId, x.SalaryYear, x.SalaryMonth }).IsUnique();
        b.Entity<SalaryPayment>().Property(x => x.GrossSalary).HasPrecision(18,2);
        b.Entity<SalaryPayment>().Property(x => x.AbsentDays).HasPrecision(8,2);
        b.Entity<SalaryPayment>().Property(x => x.HalfDays).HasPrecision(8,2);
        b.Entity<SalaryPayment>().Property(x => x.DeductionAmount).HasPrecision(18,2);
        b.Entity<SalaryPayment>().Property(x => x.PayableAmount).HasPrecision(18,2);
        b.Entity<SalaryPayment>().Property(x => x.PaidAmount).HasPrecision(18,2);
        b.Entity<DailyExpense>().Property(x => x.Amount).HasPrecision(18,2);
        b.Entity<DoctorSettlement>().HasIndex(x => new { x.SourceType, x.SourceId }).IsUnique();
        b.Entity<DoctorSettlement>().HasIndex(x => new { x.DoctorId, x.EarnedDate });
        b.Entity<DoctorSettlement>().Property(x => x.PayableAmount).HasPrecision(18,2);
        b.Entity<DoctorSettlement>().Property(x => x.PaidAmount).HasPrecision(18,2);
        b.Entity<OtBooking>().Property(x => x.DoctorPayoutAmount).HasPrecision(18,2);
        b.Entity<PharmacyPurchasePayment>().Property(x => x.Amount).HasPrecision(18,2);
        b.Entity<LabPurchaseExpense>().Property(x => x.Amount).HasPrecision(18,2);
        b.Entity<LabPurchaseExpense>().Property(x => x.PaidAmount).HasPrecision(18,2);
        b.Entity<Patient>().HasIndex(x=>new{x.Name,x.Phone});
        b.Entity<OpdVisit>().HasIndex(x=>x.VisitNumber).IsUnique();
        b.Entity<Bill>().HasIndex(x=>x.BillNumber).IsUnique();
        b.Entity<BillTypeMaster>().HasIndex(x=>x.Name).IsUnique();
        b.Entity<Bill>().Property(x=>x.GrossAmount).HasPrecision(18,2);
        b.Entity<Bill>().Property(x=>x.DiscountPercent).HasPrecision(8,2);
        b.Entity<Bill>().Property(x=>x.DiscountAmount).HasPrecision(18,2);
        b.Entity<Bill>().Property(x=>x.NetAmount).HasPrecision(18,2);
        b.Entity<Bill>().Property(x=>x.PaidAmount).HasPrecision(18,2);
        b.Entity<BillItem>().Property(x=>x.UnitPrice).HasPrecision(18,2);
        b.Entity<BillItem>().Property(x=>x.DiscountValue).HasPrecision(18,2);
        b.Entity<BillItem>().Property(x=>x.DiscountAmount).HasPrecision(18,2);
        b.Entity<BillItem>().Property(x=>x.Amount).HasPrecision(18,2);
        b.Entity<Payment>().Property(x=>x.Amount).HasPrecision(18,2);
        b.Entity<WardBed>().HasIndex(x=>x.BedNumber).IsUnique();
        b.Entity<WardBed>().Property(x=>x.CashRate).HasPrecision(18,2);
        b.Entity<WardBed>().Property(x=>x.InsuranceRate).HasPrecision(18,2);
        b.Entity<WardBed>().Property(x=>x.AyushmanRate).HasPrecision(18,2);
        b.Entity<IpdAdmission>().HasIndex(x=>x.AdmissionNumber).IsUnique();
        b.Entity<OtBooking>().HasIndex(x=>new{x.OtRoom,x.StartAtUtc,x.EndAtUtc});
        b.Entity<Medicine>().Property(x=>x.PricePerStrip).HasPrecision(18,2);
        b.Entity<LabTest>().Property(x=>x.Price).HasPrecision(18,2);
        b.Entity<LabOrderTest>().Property(x=>x.UnitPrice).HasPrecision(18,2);
        b.Entity<LabOrderTest>().Property(x=>x.DiscountValue).HasPrecision(18,2);
        b.Entity<LabOrderTest>().Property(x=>x.DiscountAmount).HasPrecision(18,2);
        b.Entity<LabOrderTest>().Property(x=>x.NetAmount).HasPrecision(18,2);
        b.Entity<AppSetting>().HasIndex(x=>x.Name).IsUnique();

        b.Entity<PharmacyDistributor>().HasIndex(x => x.Name);
        b.Entity<PharmacyMedicineType>().HasIndex(x => x.Name).IsUnique();
        b.Entity<PharmacyPurchaseInvoice>()
            .HasIndex(x => new { x.DistributorId, x.InvoiceNumber })
            .IsUnique();
        b.Entity<PharmacyPurchaseItem>()
            .HasOne(x => x.MedicineType)
            .WithMany()
            .HasForeignKey(x => x.MedicineTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        b.Entity<PharmacyPurchaseItem>()
            .HasIndex(x => new { x.ProductName, x.BatchNo, x.Hsn });

        b.Entity<PharmacyPurchaseItem>().Property(x => x.Mrp).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseItem>().Property(x => x.Rate).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseItem>().Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseItem>().Property(x => x.TaxableAmount).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseItem>().Property(x => x.CgstPercent).HasPrecision(8, 2);
        b.Entity<PharmacyPurchaseItem>().Property(x => x.CgstAmount).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseItem>().Property(x => x.SgstPercent).HasPrecision(8, 2);
        b.Entity<PharmacyPurchaseItem>().Property(x => x.SgstAmount).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseItem>().Property(x => x.TotalAmount).HasPrecision(18, 2);

        b.Entity<PharmacyPurchaseInvoice>().Property(x => x.TotalQuantity).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseInvoice>().Property(x => x.TotalDiscount).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseInvoice>().Property(x => x.TotalTaxableAmount).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseInvoice>().Property(x => x.TotalCgst).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseInvoice>().Property(x => x.TotalSgst).HasPrecision(18, 2);
        b.Entity<PharmacyPurchaseInvoice>().Property(x => x.TotalAmount).HasPrecision(18, 2);

        b.Entity<PharmacySale>().Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Entity<PharmacySaleItem>().Property(x => x.Mrp).HasPrecision(18, 2);
        b.Entity<PharmacySaleItem>().Property(x => x.UnitPrice).HasPrecision(18, 4);
        b.Entity<PharmacySaleItem>().Property(x => x.DiscountAmount).HasPrecision(18, 2);
        b.Entity<PharmacySaleItem>().Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Entity<PharmacyExpiryAction>().Property(x => x.Amount).HasPrecision(18, 2);

        b.Entity<PharmacyPurchaseInvoice>()
            .HasMany(x => x.Items)
            .WithOne(x => x.PurchaseInvoice)
            .HasForeignKey(x => x.PurchaseInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<PharmacySale>()
            .HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.PharmacySaleId)
            .OnDelete(DeleteBehavior.Cascade);
        b.Entity<Bill>().HasMany(x=>x.Items).WithOne().HasForeignKey(x=>x.BillId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<Bill>().HasMany(x=>x.Payments).WithOne().HasForeignKey(x=>x.BillId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<LabTest>().HasMany(x=>x.Components).WithOne().HasForeignKey(x=>x.LabTestId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<LabOrder>().HasMany(x=>x.Tests).WithOne().HasForeignKey(x=>x.LabOrderId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<LabOrderTest>().HasMany(x=>x.Results).WithOne().HasForeignKey(x=>x.LabOrderTestId).OnDelete(DeleteBehavior.Cascade);
    }
}
