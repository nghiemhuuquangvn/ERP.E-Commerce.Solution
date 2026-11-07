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

    /// <summary>Department ids by code (the keys are upper case); codes that match nothing are absent.</summary>
    Task<Dictionary<string, Guid>> GetIdsByCodesAsync(IReadOnlyCollection<string> upperCaseCodes, CancellationToken cancellationToken = default);
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

    /// <summary>Position ids by code (the keys are upper case); codes that match nothing are absent.</summary>
    Task<Dictionary<string, Guid>> GetIdsByCodesAsync(IReadOnlyCollection<string> upperCaseCodes, CancellationToken cancellationToken = default);
    Task<PositionTranslation?> GetTranslationAsync(Guid ownerId, string languageCode, CancellationToken cancellationToken = default);
    void Add(Position position);
    void Update(Position position);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Employee> Items, int TotalCount)> ListAsync(
        Guid? departmentId,
        int? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Employee>> GetActiveEmployeesAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Which of the given e-mails (lower case) already belong to an employee: one query for a whole import batch.</summary>
    Task<HashSet<string>> GetExistingEmailsAsync(IReadOnlyCollection<string> lowerCaseEmails, CancellationToken cancellationToken = default);

    /// <summary>Which of the given employee numbers (upper case) are taken.</summary>
    Task<HashSet<string>> GetExistingNumbersAsync(IReadOnlyCollection<string> upperCaseNumbers, CancellationToken cancellationToken = default);

    /// <summary>The highest numeric suffix among the numbers that are exactly prefix + digits digits (0 when none): where a generated number sequence continues.</summary>
    Task<int> GetMaxNumberSuffixAsync(string prefix, int digits, CancellationToken cancellationToken = default);

    /// <summary>Forgets every tracked entity (a long import commits in batches).</summary>
    void ClearTracking();
    void Add(Employee employee);
    void Update(Employee employee);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfMeasureRepository
{
    Task<UnitOfMeasure?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>The existing units among <paramref name="ids"/> (own tenant + shared reference rows); unknown ids are missing.</summary>
    Task<IReadOnlyList<UnitOfMeasure>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    /// <summary>The existing units whose code equals one of the (any case) codes (own tenant + shared reference rows).</summary>
    Task<IReadOnlyList<UnitOfMeasure>> GetByCodesAsync(IReadOnlyCollection<string> codes, CancellationToken cancellationToken = default);
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

public interface ICurrencyRepository
{
    Task<Currency?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Currency> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<Currency?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(Currency currency);
    void Update(Currency currency);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IExchangeRateRepository
{
    Task<ExchangeRate?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ExchangeRate> Items, int TotalCount)> ListAsync(
        Guid? fromCurrencyId,
        Guid? toCurrencyId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    // Business rule: the record with the latest EffectiveDate <= asOfDate.
    Task<ExchangeRate?> GetEffectiveRateAsync(
        Guid fromCurrencyId,
        Guid toCurrencyId,
        DateTime asOfDate,
        CancellationToken cancellationToken = default);
    void Add(ExchangeRate exchangeRate);
    void Update(ExchangeRate exchangeRate);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IPaymentTermRepository
{
    Task<PaymentTerm?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<PaymentTerm> Items, int TotalCount)> ListAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string code, CancellationToken cancellationToken = default);
    void Add(PaymentTerm paymentTerm);
    void Update(PaymentTerm paymentTerm);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
