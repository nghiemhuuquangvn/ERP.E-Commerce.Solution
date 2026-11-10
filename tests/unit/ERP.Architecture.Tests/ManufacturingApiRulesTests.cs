using System.Reflection;
using ERP.MFG.API.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ERP.Architecture.Tests;

/// <summary>Shape rules of the Manufacturing API from the BA spec (§5.1) that no feature test names.</summary>
public class ManufacturingApiRulesTests
{
    private static readonly Type[] DocumentControllers = [typeof(OrdersController), typeof(StockDocumentsController), typeof(OperationsController)];

    [Fact] // TC-GEN-002 (BR-GEN-01): a posted document cannot be deleted - there is no DELETE endpoint on orders, stock documents, operations or reports
    public void TC_GEN_002_No_Document_Endpoint_Deletes()
    {
        foreach (var controller in DocumentControllers)
            controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes<HttpDeleteAttribute>().Any())
                .Should().BeEmpty($"{controller.Name} must not delete documents (BR-GEN-01: cancel, reverse or close instead)");
    }

    [Fact] // TC-GEN-008 (BR-GEN-08): the posting date is the plant's today; a request carries no date, so a future (or past) posting date cannot be sent
    public void TC_GEN_008_Posting_Requests_Carry_No_Posting_Date()
    {
        var bodies = new[] { typeof(OrdersController.MaterialBody), typeof(OrdersController.ReceiptBody), typeof(OperationsController.ReportBody), typeof(OperationsController.ManualTimeBody) };
        foreach (var body in bodies)
            body.GetProperties().Select(p => p.Name).Should().NotContain(n => n.Contains("PostingDate", StringComparison.OrdinalIgnoreCase) || n == "Date",
                $"{body.Name} must not let the caller choose the posting date");
    }
}
