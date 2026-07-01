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
