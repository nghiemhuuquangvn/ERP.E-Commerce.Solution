using System.Globalization;
using System.Text.RegularExpressions;
using ERP.MST.Domain.Enums;
using ERP.Shared.Streaming;

namespace ERP.MST.Application.Employees.Import;

/// <summary>One valid row of the employee CSV (codes are upper case, the e-mail lower case).</summary>
public sealed record EmployeeImportData(
    int Line, string? EmployeeNumber, string FirstName, string LastName, string? Email, string? Phone,
    string DepartmentCode, string? PositionCode, DateTime DateOfHire, EmploymentType EmploymentType)
{
    /// <summary>The key shown in the result lines: the number when given, else the e-mail, else the name.</summary>
    public string Key => EmployeeNumber ?? Email ?? $"{FirstName} {LastName}";
}

public sealed record EmployeeImportParseResult(EmployeeImportData? Data, string? Error, string? Key);

/// <summary>
/// The row rules of the employee import. Columns (normalised header names): employeenumber, firstname, lastname, email, phone, departmentcode,
/// positioncode, dateofhire, employmenttype. They follow the hire form (names up to 100 characters, a valid e-mail, a phone up to 20 characters,
/// a hire date no later than 30 days ahead). A row's problem is returned as text, never thrown.
/// </summary>
public static class EmployeeImportParser
{
    public static readonly string[] RequiredColumns = { "firstname", "lastname", "departmentcode", "dateofhire" };

    public const string TemplateCsv =
        "employeeNumber,firstName,lastName,email,phone,departmentCode,positionCode,dateOfHire,employmentType\r\n" +
        ",An,Nguyen,an.nguyen@example.com,0900000001,SALES,SALES-REP,2024-03-01,FullTime\r\n" +
        "EMP-2022-00042,Binh,Tran,binh.tran@example.com,,FIN,,15/06/2022,PartTime\r\n";

    private static readonly Regex NumberPattern = new("^[A-Z0-9][A-Z0-9-]{0,49}$", RegexOptions.Compiled);
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly string[] DateFormats = { "yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "yyyy/MM/dd" };

    public static EmployeeImportParseResult Parse(CsvImportRow row, DateTime today)
    {
        var number = row.Get("employeenumber").ToUpperInvariant();
        var first = row.Get("firstname");
        var last = row.Get("lastname");
        var email = row.Get("email").ToLowerInvariant();
        var key = number.Length > 0 ? number : email.Length > 0 ? email : $"{first} {last}".Trim();

        if (number.Length > 0 && !NumberPattern.IsMatch(number))
            return new(null, "The employee number may only hold letters, digits and '-', up to 50 characters.", key);
        if (first.Length == 0 || first.Length > 100) return new(null, "The first name is required (up to 100 characters).", key);
        if (last.Length == 0 || last.Length > 100) return new(null, "The last name is required (up to 100 characters).", key);
        if (email.Length > 0 && (email.Length > 256 || !EmailPattern.IsMatch(email))) return new(null, $"'{email}' is not a valid e-mail address.", key);

        var phone = row.Get("phone");
        if (phone.Length > 20) return new(null, "The phone is longer than 20 characters.", key);

        var department = row.Get("departmentcode").ToUpperInvariant();
        if (department.Length == 0 || department.Length > 50) return new(null, "The department code is required.", key);
        var position = row.Get("positioncode").ToUpperInvariant();
        if (position.Length > 50) return new(null, "The position code is longer than 50 characters.", key);

        var dateText = row.Get("dateofhire");
        if (dateText.Length == 0) return new(null, "The hire date is required (yyyy-MM-dd or dd/MM/yyyy).", key);
        if (!DateTime.TryParseExact(dateText, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hired))
            return new(null, $"'{dateText}' is not a date (use yyyy-MM-dd or dd/MM/yyyy).", key);
        if (hired > today.AddDays(30)) return new(null, "The hire date is more than 30 days in the future.", key);
        if (hired.Year < 1950) return new(null, "The hire date is before 1950.", key);

        var typeText = row.Get("employmenttype");
        var type = EmploymentType.FullTime;
        if (typeText.Length > 0 && (!Enum.TryParse(typeText, ignoreCase: true, out type) || !Enum.IsDefined(type) || int.TryParse(typeText, out _)))
            return new(null, $"Unknown employment type '{typeText}' (use FullTime, PartTime or Contract).", key);

        return new(new EmployeeImportData(
            row.Line, number.Length == 0 ? null : number, first, last, email.Length == 0 ? null : email, phone.Length == 0 ? null : phone,
            department, position.Length == 0 ? null : position, hired, type), null, key);
    }
}
