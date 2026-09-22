using System.Security.Cryptography;
namespace KrishavERP.Api;
public static class PasswordUtil
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password,salt,100000,HashAlgorithmName.SHA256,32);
        return Convert.ToBase64String(salt)+"."+Convert.ToBase64String(hash);
    }
    public static bool Verify(string password,string stored)
    {
        var p=stored.Split('.');
        if(p.Length!=2) return false;
        var salt=Convert.FromBase64String(p[0]);
        var expected=Convert.FromBase64String(p[1]);
        var actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,100000,HashAlgorithmName.SHA256,32);
        return CryptographicOperations.FixedTimeEquals(expected,actual);
    }
}
public static class DbSeeder
{
    public static readonly string[] Modules=
    {
        "DASHBOARD","DOCTOR","PATIENT","OPD","BILL","IPD","OT","PHARMACY","LAB","SETTINGS","BED","ROLE","PERMISSION","REPORTING","DISCOUNT","REFERRAL","FOLLOWUP","PATIENT_SOURCE","BILL_TYPE","STAFF","EXPENSE"
    }
    ;
    public static void Seed(AppDbContext db)
    {
        AppRole adminRole;
        if(!db.AppRoles.Any())
        {
            adminRole=new AppRole
            {
                Name="Administrator",Description="Full system access"
            }
            ;
            db.AppRoles.Add(adminRole);
            db.SaveChanges();
            foreach(var m in Modules)db.RolePermissions.Add(new RolePermission{RoleId=adminRole.Id,Module=m,CanView=true,CanAdd=true,CanEdit=true,CanDelete=true});
            db.SaveChanges();
        }
        else adminRole=db.AppRoles.First();
        if(!db.AppUsers.Any())db.AppUsers.Add(new AppUser{Username="admin",DisplayName="Administrator",PasswordHash=PasswordUtil.Hash("Admin@123"),RoleId=adminRole.Id});
        if(!db.AppSettings.Any())
        {
            db.AppSettings.AddRange(                 new AppSetting{Name="Hospital Name",Value="Krishav Health Care",Type="Branding"},                 new AppSetting{Name="Hospital Address",Value="Housing Board, J.K. Pur Road, Rayagada - 765001, Odisha",Type="Branding"},                 new AppSetting{Name="Hospital Phone",Value="8001011818 / 9002561818",Type="Branding"},                 new AppSetting{Name="Hospital Logo",Value="",Type="Branding"},                 new AppSetting{Name="OPD Header",Value="",Type="Branding"},                 new AppSetting{Name="Lab Header",Value="",Type="Branding"},                 new AppSetting{Name="Lab Signature",Value="",Type="Branding"},                 new AppSetting{Name="Lab Signatory Name",Value="",Type="Generic"},                 new AppSetting{Name="Lab Signatory Qualification",Value="",Type="Generic"},                 new AppSetting{Name="Pharmacy Header",Value="",Type="Branding"},                 new AppSetting{Name="Blank Prescription Header",Value="",Type="Branding"},                 new AppSetting{Name="Hospital Charge",Value="100",Type="Billing"},                 new AppSetting{Name="Emergency Rate",Value="500",Type="Billing"},                 new AppSetting{Name="Oxygen Charge Cash",Value="300",Type="Billing"},                 new AppSetting{Name="Oxygen Charge Insurance",Value="400",Type="Billing"},                 new AppSetting{Name="Oxygen Charge Ayushman",Value="350",Type="Billing"}             );
        }
        if(!db.DocumentCategories.Any())
        {
            db.DocumentCategories.AddRange(
                new DocumentCategory { Name = "Prescription" },
                new DocumentCategory { Name = "Aadhaar" },
                new DocumentCategory { Name = "Outside Lab Report" },
                new DocumentCategory { Name = "Insurance" },
                new DocumentCategory { Name = "Other" });
        }
        if(!db.StaffDesignations.Any())
        {
            foreach(var name in new[]{"Administrator","Receptionist","Nurse","Pharmacist","Lab Technician","Accountant","Housekeeping","Security","Ward Attendant","Other"})
            {
                db.StaffDesignations.Add(new StaffDesignation{Name=name,IsActive=true});
            }
        }

        if(!db.PatientSources.Any())
        {
            foreach(var name in new[]
            {
                "Walk-in",
                "Existing Patient",
                "Doctor Referral",
                "Hospital Referral",
                "Staff Referral",
                "Patient Referral",
                "Google",
                "Facebook",
                "Instagram",
                "WhatsApp",
                "Health Camp",
                "Banner-Hoarding",
                "Corporate",
                "Insurance-TPA",
                "Ambulance",
                "Other"
            })
            {
                db.PatientSources.Add(new PatientSource
                {
                    Name = name,
                    IsActive = true
                });
            }
        }

        if(!db.DiscountTypes.Any())
        {
            var names = new (string Name,string Scope)[]
            {
                ("Company Default Discount","Bulk"),
                ("Management Approved Discount","Bulk"),
                ("Senior Citizen","Individual"),
                ("BPL","Individual"),
                ("Staff","Individual"),
                ("Staff Family","Individual"),
                ("Doctor Recommended","Individual"),
                ("Package Discount","Bulk"),
                ("Corporate/Company Agreement","Bulk"),
                ("Insurance/TPA","Bulk"),
                ("Promotional/Campaign","Bulk"),
                ("Other","Individual")
            };
            foreach(var item in names)
                db.DiscountTypes.Add(new DiscountType{Name=item.Name,Scope=item.Scope,DiscountMode="Percent",Value=0,IsActive=false});
            db.SaveChanges();
        }
        if(!db.BillTypes.Any())
        {
            db.BillTypes.AddRange(
                new BillTypeMaster { Name = "OPD", IsBillable = true, IsOpdType = true },
                new BillTypeMaster { Name = "Emergency", IsBillable = true, IsOpdType = true },
                new BillTypeMaster { Name = "Dental", IsBillable = true, IsOpdType = true },
                new BillTypeMaster { Name = "Appointment", IsBillable = false, IsOpdType = true },
                new BillTypeMaster { Name = "IPD", IsBillable = true },
                new BillTypeMaster { Name = "OT", IsBillable = true },
                new BillTypeMaster { Name = "Lab", IsBillable = true },
                new BillTypeMaster { Name = "Pharmacy", IsBillable = true },
                new BillTypeMaster { Name = "Dressing", IsBillable = true }
            );
        }

        if(!db.ReportCategories.Any())
        {
            foreach (var name in new[] { "OPD", "IPD", "Lab", "Pharmacy", "Emergency", "OT", "Dressing", "Dental" })
            {
                db.ReportCategories.Add(new ReportCategory { Name = name });
            }
        }
        if(!db.PharmacyMedicineTypes.Any())
        {
            db.PharmacyMedicineTypes.AddRange(
                new PharmacyMedicineType { Name = "Tablet" },
                new PharmacyMedicineType { Name = "Cream" },
                new PharmacyMedicineType { Name = "Injection" },
                new PharmacyMedicineType { Name = "Spray" },
                new PharmacyMedicineType { Name = "Syrup" },
                new PharmacyMedicineType { Name = "Powder" },
                new PharmacyMedicineType { Name = "Other" });
        }
        if(!db.WardBeds.Any())
        {
            for(int i=1;i<=18;i++)db.WardBeds.Add(new WardBed{BedNumber=$"BED-{i:00}",RoomType="Bed",CashRate=1200,InsuranceRate=1500,AyushmanRate=1300});
        }
        db.SaveChanges();
    }
}
