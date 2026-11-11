using ERP.MFG.Domain.Common;
using ERP.MFG.Domain.Entities;

namespace ERP.MFG.Domain.Engineering;

/// <summary>One value an inspector typed: a measurement of one sample unit, or the pass / fail of an attribute.</summary>
public sealed record InspectionResultInput(Guid CharacteristicId, int SampleNo, decimal? Value, bool? Passed, string? Note);

/// <summary>How one characteristic came out: every required value present, and whether all of them passed.</summary>
public sealed record CharacteristicOutcome(Guid CharacteristicId, string Name, bool IsCritical, bool Complete, bool Passed);

/// <summary>The verdict of an inspection (BR-QC-04). <see cref="Missing"/> lists the characteristics that still lack values: no verdict is given then.</summary>
public sealed record QualityOutcome(bool Passed, IReadOnlyList<CharacteristicOutcome> Characteristics, IReadOnlyList<string> Missing, int FailedCritical, int FailedMinor);

/// <summary>Sample size and evaluation of an inspection (BR-QC-03, BR-QC-04). Pure: the one place the numbers are computed.</summary>
public static class QualityRules
{
    /// <summary>
    /// BR-QC-03: Fixed = <c>min(n, lot)</c>, Percent = <c>ceil(lot x p / 100)</c> (never above the lot), Full = the lot. A lot of zero has no sample.
    /// </summary>
    public static decimal SampleSize(SamplingMethod method, decimal? value, decimal lotQty)
    {
        if (lotQty <= 0) return 0m;
        return method switch
        {
            SamplingMethod.Fixed => Math.Min(value ?? 0m, lotQty),
            SamplingMethod.Percent => Math.Min(Math.Ceiling(lotQty * (value ?? 0m) / 100m), lotQty),
            _ => lotQty,
        };
    }

    /// <summary>How many sample units get a value: a part of a unit still is a unit to look at. At least one.</summary>
    public static int Units(decimal sampleQty) => Math.Max(1, (int)Math.Ceiling(sampleQty));

    /// <summary>
    /// BR-QC-04: a measurement characteristic needs one value per sample unit and passes when all lie in range; an attribute needs one pass / fail. The lot
    /// passes when every critical characteristic passes and the number of failed minor ones is at most <paramref name="allowedMinorDefects"/>.
    /// </summary>
    public static QualityOutcome Evaluate(IReadOnlyCollection<InspectionCharacteristic> characteristics, IEnumerable<InspectionResult> results, decimal sampleQty, int allowedMinorDefects)
    {
        var units = Units(sampleQty);
        var byCharacteristic = results.GroupBy(r => r.CharacteristicId).ToDictionary(g => g.Key, g => g.ToList());
        var outcomes = new List<CharacteristicOutcome>();
        var missing = new List<string>();

        foreach (var c in characteristics)
        {
            var entered = byCharacteristic.GetValueOrDefault(c.Id) ?? [];
            var required = c.Kind == CharacteristicKind.Measurement ? units : 1;
            var complete = Enumerable.Range(1, required).All(no => entered.Any(r => r.SampleNo == no));
            if (!complete) missing.Add(c.Name);
            outcomes.Add(new CharacteristicOutcome(c.Id, c.Name, c.IsCritical, complete, complete && entered.Where(r => r.SampleNo <= required).All(r => r.IsPassed)));
        }

        var failedCritical = outcomes.Count(o => o.IsCritical && o.Complete && !o.Passed);
        var failedMinor = outcomes.Count(o => !o.IsCritical && o.Complete && !o.Passed);
        var passed = missing.Count == 0 && failedCritical == 0 && failedMinor <= allowedMinorDefects;
        return new QualityOutcome(passed, outcomes, missing, failedCritical, failedMinor);
    }
}
