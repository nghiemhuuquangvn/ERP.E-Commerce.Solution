namespace ERP.HRM.Domain.Enums;

public enum EmploymentStatus
{
    Active = 0,
    OnLeave = 1,
    Terminated = 2
}

public enum EmploymentType
{
    FullTime = 0,
    PartTime = 1,
    Contract = 2
}

public enum LeaveType
{
    Annual = 0,
    Sick = 1,
    Unpaid = 2,
    Maternity = 3,
    Paternity = 4
}

public enum LeaveStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}

public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Cancelled = 2
}

public enum AttendanceStatus
{
    Present = 0,
    Absent = 1,
    Late = 2,
    HalfDay = 3,
    OnLeave = 4,
    Holiday = 5
}
