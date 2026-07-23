using ERP.MST.Domain.Entities;

namespace ERP.MST.Application.Interfaces;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Department> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<DepartmentTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default);
    void Add(Department department);
    void Update(Department department);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPositionRepository
{
    Task<Position?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Position> Items, int TotalCount)> ListAsync(
        Guid? departmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<PositionTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default);
    void Add(Position position);
    void Update(Position position);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Employee> Items, int TotalCount)> ListAsync(
        Guid? departmentId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> GetActiveEmployeesAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);
    void Add(Employee employee);
    void Update(Employee employee);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfMeasureRepository
{
    Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<UnitOfMeasure> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<UnitOfMeasureTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default);
    void Add(UnitOfMeasure unitOfMeasure);
    void Update(UnitOfMeasure unitOfMeasure);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICountryRepository
{
    Task<Country?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Country> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<CountryTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default);
    void Add(Country country);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProvinceRepository
{
    Task<Province?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Province> Items, int TotalCount)> ListAsync(
        Guid countryId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<ProvinceTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default);
    void Add(Province province);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IDistrictRepository
{
    Task<District?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<District> Items, int TotalCount)> ListAsync(
        Guid provinceId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<DistrictTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default);
    void Add(District district);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IWardRepository
{
    Task<Ward?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Ward> Items, int TotalCount)> ListAsync(
        Guid districtId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<WardTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default);
    void Add(Ward ward);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ISupportedLanguageRepository
{
    Task<SupportedLanguage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<SupportedLanguage> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(SupportedLanguage language);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICompanyRepository
{
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Company> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(Company company);
    void Update(Company company);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Branch> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(Branch branch);
    void Update(Branch branch);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IDivisionRepository
{
    Task<Division?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Division> Items, int TotalCount)> ListAsync(
        Guid? branchId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(Division division);
    void Update(Division division);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Team> Items, int TotalCount)> ListAsync(
        Guid? departmentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(Team team);
    void Update(Team team);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IBusinessUnitRepository
{
    Task<BusinessUnit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<BusinessUnit> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(BusinessUnit businessUnit);
    void Update(BusinessUnit businessUnit);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ICostCenterRepository
{
    Task<CostCenter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<CostCenter> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(CostCenter costCenter);
    void Update(CostCenter costCenter);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProfitCenterRepository
{
    Task<ProfitCenter?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ProfitCenter> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(ProfitCenter profitCenter);
    void Update(ProfitCenter profitCenter);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IOrganizationCalendarRepository
{
    Task<OrganizationCalendar?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<OrganizationCalendar> Items, int TotalCount)> ListAsync(
        Guid? companyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    void Add(OrganizationCalendar calendar);
    void Update(OrganizationCalendar calendar);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IHolidayRepository
{
    Task<Holiday?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Holiday> Items, int TotalCount)> ListAsync(
        Guid calendarId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    void Add(Holiday holiday);
    void Update(Holiday holiday);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IEmployeeTeamAssignmentRepository
{
    Task<EmployeeTeamAssignment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<EmployeeTeamAssignment> Items, int TotalCount)> ListAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    void Add(EmployeeTeamAssignment assignment);
    void Update(EmployeeTeamAssignment assignment);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
