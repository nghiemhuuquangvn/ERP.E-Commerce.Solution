using System.Text;
using ERP.MST.Application.Employees.Import;
using ERP.MST.Application.Interfaces;
using ERP.MST.Domain.Entities;
using ERP.MST.Domain.Enums;
using ERP.MST.Domain.Events;
using ERP.Shared.Infrastructure.Exceptions;
using ERP.Shared.Streaming;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ERP.MST.UnitTests.Employees;

/// <summary>The streaming employee import: row rules, duplicates, code lookups, number generation, batches, dry run, the hired event.</summary>
public class EmployeeImportTests
{
    private const string Header = "employeeNumber,firstName,lastName,email,phone,departmentCode,positionCode,dateOfHire,employmentType\n";

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
    }

    private sealed class Harness
    {
        public readonly Mock<IEmployeeRepository> Employees = new();
        public readonly Mock<IDepartmentRepository> Departments = new();
        public readonly Mock<IPositionRepository> Positions = new();
        public readonly List<Employee> Added = new();
        public HashSet<string> ExistingEmails = new();
        public HashSet<string> ExistingNumbers = new();
        public int MaxSuffix;
        public Dictionary<string, Guid> DepartmentIds = new() { ["SALES"] = Guid.NewGuid(), ["FIN"] = Guid.NewGuid() };
        public Dictionary<string, Guid> PositionIds = new() { ["REP"] = Guid.NewGuid() };
        public readonly ImportProgressRegistry Registry;
        public readonly ImportLimits Limits;

        public Harness(int batchSize = 200)
        {
            Limits = new ImportLimits { BatchSize = batchSize };
            Registry = new ImportProgressRegistry(Options.Create(Limits));

            Employees.Setup(e => e.GetExistingEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> mails, CancellationToken _) => new HashSet<string>(mails.Where(ExistingEmails.Contains)));
            Employees.Setup(e => e.GetExistingNumbersAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> numbers, CancellationToken _) => new HashSet<string>(numbers.Where(ExistingNumbers.Contains)));
            Employees.Setup(e => e.GetMaxNumberSuffixAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(() => MaxSuffix);
            Employees.Setup(e => e.Add(It.IsAny<Employee>())).Callback<Employee>(Added.Add);
            Employees.Setup(e => e.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Departments.Setup(d => d.GetIdsByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> codes, CancellationToken _) => codes.Where(DepartmentIds.ContainsKey).ToDictionary(c => c, c => DepartmentIds[c]));
            Positions.Setup(p => p.GetIdsByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> codes, CancellationToken _) => codes.Where(PositionIds.ContainsKey).ToDictionary(c => c, c => PositionIds[c]));
        }

        public ImportEmployeesStreamHandler Handler() =>
            new(Employees.Object, Departments.Object, Positions.Object, Registry, Options.Create(Limits), Mock.Of<ILogger<ImportEmployeesStreamHandler>>(), new FixedClock());

        public async Task<List<ImportLine>> Run(string csv, bool dryRun = false, Guid? importId = null, CancellationToken ct = default)
        {
            var command = new ImportEmployeesStreamCommand
            {
                Body = new MemoryStream(Encoding.UTF8.GetBytes(csv)),
                ContentLength = Encoding.UTF8.GetByteCount(csv),
                TenantId = "tenant",
                UserId = "user",
                ImportId = importId ?? Guid.NewGuid(),
                DryRun = dryRun,
            };
            var lines = new List<ImportLine>();
            await foreach (var line in Handler().Handle(command, ct)) lines.Add(line);
            return lines;
        }
    }

    private static List<ImportRowLine> Rows(IEnumerable<ImportLine> lines) => lines.OfType<ImportRowLine>().ToList();
    private static ImportSummaryLine Summary(IEnumerable<ImportLine> lines) => lines.OfType<ImportSummaryLine>().Single();

    // ── happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Valid_rows_create_active_employees_with_department_position_profile_and_an_import_hired_event()
    {
        var h = new Harness();

        var lines = await h.Run(Header + "EMP-9-1,An,Nguyen,An.Nguyen@Example.com,0900000001,sales,rep,2024-03-01,PartTime\n");

        Rows(lines).Single().Status.Should().Be("created");
        var employee = h.Added.Single();
        employee.EmployeeNumber.Should().Be("EMP-9-1");
        (employee.FirstName, employee.LastName).Should().Be(("An", "Nguyen"));
        employee.Email.Should().Be("an.nguyen@example.com");
        employee.Phone.Should().Be("0900000001");
        employee.DepartmentId.Should().Be(h.DepartmentIds["SALES"]);
        employee.PositionId.Should().Be(h.PositionIds["REP"]);
        employee.EmploymentType.Should().Be((int)EmploymentType.PartTime);
        employee.EmploymentStatus.Should().Be(0);
        employee.DateOfHire.Should().Be(new DateTime(2024, 3, 1));
        employee.DomainEvents.OfType<EmployeeHiredEvent>().Should().ContainSingle().Which.Source.Should().Be("import");
        (Summary(lines).Created, Summary(lines).Failed).Should().Be((1, 0));
    }

    [Fact]
    public async Task A_blank_number_is_generated_above_the_highest_generated_one_and_never_repeats_in_the_file()
    {
        var h = new Harness();
        h.MaxSuffix = 41;

        var lines = await h.Run(Header + ",A,One,,,SALES,,2026-01-05,\n,B,Two,,,SALES,,2026-02-05,\nEMP-2026-00044,C,Three,,,SALES,,2026-03-05,\n,D,Four,,,SALES,,2026-04-05,\n");

        h.Added.Select(e => e.EmployeeNumber).Should().Equal("EMP-2026-00042", "EMP-2026-00043", "EMP-2026-00044", "EMP-2026-00045");
        Rows(lines).Select(r => r.Key).Should().Equal(h.Added.Select(e => e.EmployeeNumber));
    }

    [Fact]
    public async Task Date_formats_both_iso_and_day_first_are_read_and_the_template_is_valid()
    {
        var h = new Harness();
        h.DepartmentIds["SALES"] = Guid.NewGuid();

        var lines = await h.Run(EmployeeImportParser.TemplateCsv.Replace("SALES-REP", "REP"));

        Rows(lines).Select(r => r.Status).Should().Equal("created", "created");
        h.Added[1].DateOfHire.Should().Be(new DateTime(2022, 6, 15));
        h.Added[1].EmploymentType.Should().Be((int)EmploymentType.PartTime);
    }

    // ── skipped and invalid rows ─────────────────────────────────────────

    [Fact]
    public async Task An_existing_email_or_number_and_a_repeat_in_the_file_are_skipped_with_a_reason()
    {
        var h = new Harness();
        h.ExistingEmails.Add("old@example.com");
        h.ExistingNumbers.Add("EMP-OLD");

        var lines = await h.Run(Header +
            ",A,One,old@example.com,,SALES,,2025-01-01,\n" +          // existing e-mail
            "emp-old,B,Two,,,SALES,,2025-01-01,\n" +                   // existing number (any case)
            ",C,Three,new@example.com,,SALES,,2025-01-01,\n" +         // created
            ",D,Four,NEW@example.com,,SALES,,2025-01-01,\n" +          // repeats the e-mail of the row above
            "EMP-X,E,Five,,,SALES,,2025-01-01,\n" +                    // created
            "EMP-X,F,Six,,,SALES,,2025-01-01,\n");                     // repeats the number

        var rows = Rows(lines);
        rows.Select(r => r.Status).Should().Equal("skipped", "skipped", "created", "skipped", "created", "skipped");
        rows[0].Message.Should().Contain("e-mail already exists");
        rows[1].Message.Should().Contain("number already exists");
        rows[3].Message.Should().Contain("e-mail repeats");
        rows[5].Message.Should().Contain("number repeats");
        h.Added.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(",,Nguyen,,,SALES,,2025-01-01,", "first name is required")]
    [InlineData(",An,,,,SALES,,2025-01-01,", "last name is required")]
    [InlineData(",An,Nguyen,not-an-email,,SALES,,2025-01-01,", "not a valid e-mail")]
    [InlineData(",An,Nguyen,,012345678901234567890,SALES,,2025-01-01,", "phone is longer than 20")]
    [InlineData(",An,Nguyen,,,,,2025-01-01,", "department code is required")]
    [InlineData(",An,Nguyen,,,SALES,,,", "hire date is required")]
    [InlineData(",An,Nguyen,,,SALES,,31-12-2025,", "not a date")]
    [InlineData(",An,Nguyen,,,SALES,,2026-12-31,", "more than 30 days")]
    [InlineData(",An,Nguyen,,,SALES,,2025-01-01,Freelance", "Unknown employment type")]
    [InlineData(",An,Nguyen,,,SALES,,2025-01-01,7", "Unknown employment type")]
    [InlineData("bad number!,An,Nguyen,,,SALES,,2025-01-01,", "employee number may only hold")]
    public async Task An_invalid_row_is_reported_with_its_line_and_nothing_is_created(string row, string reason)
    {
        var h = new Harness();

        var lines = await h.Run(Header + row + "\n");

        var line = Rows(lines).Single();
        (line.Line, line.Status).Should().Be((2, "error"));
        line.Message.Should().Contain(reason);
        h.Added.Should().BeEmpty();
        Summary(lines).Failed.Should().Be(1);
    }

    [Fact]
    public async Task An_unknown_department_or_position_code_fails_the_row()
    {
        var h = new Harness();

        var lines = await h.Run(Header + ",A,One,,,NOPE,,2025-01-01,\n,B,Two,,,SALES,GHOST,2025-01-01,\n");

        var rows = Rows(lines);
        rows.Select(r => r.Status).Should().Equal("error", "error");
        rows[0].Message.Should().Contain("department code 'NOPE'");
        rows[1].Message.Should().Contain("position code 'GHOST'");
        h.Added.Should().BeEmpty();
    }

    // ── dry run, batches, failures ───────────────────────────────────────

    [Fact]
    public async Task A_dry_run_reports_would_create_and_saves_nothing()
    {
        var h = new Harness();

        var lines = await h.Run(Header + ",A,One,,,SALES,,2025-01-01,\n,B,,,,SALES,,2025-01-01,\n", dryRun: true);

        Rows(lines).Select(r => r.Status).Should().Equal("would-create", "error");
        Summary(lines).DryRun.Should().BeTrue();
        h.Added.Should().BeEmpty();
        h.Employees.Verify(e => e.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Rows_are_saved_in_batches_and_the_tracker_is_cleared_after_each_and_lookups_run_once_per_batch()
    {
        var h = new Harness(batchSize: 2);
        var csv = Header + string.Concat(Enumerable.Range(1, 5).Select(i => $",E{i},Batch,e{i}@example.com,,SALES,,2025-01-01,\n"));

        var lines = await h.Run(csv);

        Summary(lines).Created.Should().Be(5);
        h.Employees.Verify(e => e.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));   // 2 + 2 + 1
        h.Employees.Verify(e => e.ClearTracking(), Times.Exactly(3));
        h.Employees.Verify(e => e.GetExistingEmailsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        h.Departments.Verify(d => d.GetIdsByCodesAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task A_batch_that_cannot_be_saved_is_reported_and_stops_the_import_without_leaking_the_exception_text()
    {
        var h = new Harness(batchSize: 2);
        h.Employees.SetupSequence(e => e.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1).ThrowsAsync(new Exception("db down"));
        var csv = Header + string.Concat(Enumerable.Range(1, 6).Select(i => $",E{i},Fail,,,SALES,,2025-01-01,\n"));

        var lines = await h.Run(csv);

        lines.Last().Should().BeOfType<ImportErrorLine>().Which.Message.Should().NotContain("db down");
        lines.OfType<ImportSummaryLine>().Should().BeEmpty();
        var rows = Rows(lines);
        rows.Select(r => r.Status).Should().Equal("created", "created", "error", "error");
        h.Employees.Verify(e => e.ClearTracking(), Times.AtLeast(2));
    }

    [Fact]
    public async Task A_wrong_header_is_a_business_rule_error_and_the_second_run_of_a_busy_user_is_refused()
    {
        var h = new Harness();

        var wrong = async () => await h.Run("name,mail\nA,B\n");
        await wrong.Should().ThrowAsync<BusinessRuleException>();

        h.Registry.BeginRun("tenant", "user", Guid.NewGuid());
        var busy = async () => await h.Run(Header + ",A,One,,,SALES,,2025-01-01,\n");
        await busy.Should().ThrowAsync<BusinessRuleException>().Where(e => e.RuleName == "Import.Busy");
    }

    [Fact]
    public async Task Progress_ends_with_the_final_counts_for_the_subscriber()
    {
        var h = new Harness(batchSize: 2);
        var importId = Guid.NewGuid();
        var session = h.Registry.GetOrCreate("tenant", "user", importId);
        await using var subscription = session.SubscribeAsync(CancellationToken.None).GetAsyncEnumerator();
        var first = subscription.MoveNextAsync().AsTask();

        await h.Run(Header + ",A,One,,,SALES,,2025-01-01,\n,B,Two,,,SALES,,2025-01-01,\n,C,Three,,,SALES,,2025-01-01,\n", importId: importId);

        var seen = new List<ImportProgress>();
        if (await first) seen.Add(subscription.Current);
        while (await subscription.MoveNextAsync()) seen.Add(subscription.Current);
        seen.Last().State.Should().Be(ImportStates.Completed);
        seen.Last().Created.Should().Be(3);
    }
}
