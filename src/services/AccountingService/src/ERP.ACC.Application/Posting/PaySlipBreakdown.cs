namespace ERP.ACC.Application.Posting;

/// <summary>
/// The insurance and tax split of a pay slip (ROADMAP P1-7): what is withheld from the employee, what the employer adds on top,
/// and the personal income tax. All zero for a slip without a breakdown.
/// </summary>
public record PaySlipBreakdown(
    decimal EmployeeSocial,
    decimal EmployeeHealth,
    decimal EmployeeUnemployment,
    decimal EmployerSocial,
    decimal EmployerHealth,
    decimal EmployerUnemployment,
    decimal IncomeTax)
{
    public decimal EmployeeInsurance => EmployeeSocial + EmployeeHealth + EmployeeUnemployment;
    public decimal EmployerInsurance => EmployerSocial + EmployerHealth + EmployerUnemployment;

    public bool HasValues => EmployeeInsurance + EmployerInsurance + IncomeTax > 0m;
}
