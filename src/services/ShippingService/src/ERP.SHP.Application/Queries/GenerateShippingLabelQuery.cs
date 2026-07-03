using ERP.SHP.Application.Interfaces;
using MediatR;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace ERP.SHP.Application.Queries;

public class GenerateShippingLabelQuery : IRequest<byte[]>
{
    public Guid ShipmentId { get; set; }
}

public class GenerateShippingLabelQueryHandler : IRequestHandler<GenerateShippingLabelQuery, byte[]>
{
    private readonly IShipmentRepository _shipments;
    public GenerateShippingLabelQueryHandler(IShipmentRepository shipments) => _shipments = shipments;

    public async Task<byte[]> Handle(GenerateShippingLabelQuery request, CancellationToken cancellationToken)
    {
        var shipment = await _shipments.GetByIdAsync(request.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {request.ShipmentId} not found.");

        using var document = new PdfDocument();
        var page = document.AddPage();
        page.Width = XUnit.FromMillimeter(100);
        page.Height = XUnit.FromMillimeter(150);

        using var gfx = XGraphics.FromPdfPage(page);
        var titleFont = new XFont("Arial", 14, XFontStyleEx.Bold);
        var labelFont = new XFont("Arial", 9, XFontStyleEx.Bold);
        var textFont = new XFont("Arial", 10, XFontStyleEx.Regular);
        var trackingFont = new XFont("Arial", 16, XFontStyleEx.Bold);

        double y = 15;
        gfx.DrawString(shipment.CarrierName, titleFont, XBrushes.Black, new XPoint(10, y));
        y += 20;

        gfx.DrawString("TRACKING NUMBER", labelFont, XBrushes.Black, new XPoint(10, y));
        y += 16;
        gfx.DrawString(shipment.TrackingNumber ?? "(not assigned)", trackingFont, XBrushes.Black, new XPoint(10, y));
        y += 24;

        gfx.DrawLine(XPens.Black, 10, y, page.Width.Point - 10, y);
        y += 14;

        gfx.DrawString("SHIP TO", labelFont, XBrushes.Black, new XPoint(10, y));
        y += 14;
        gfx.DrawString(shipment.RecipientName, textFont, XBrushes.Black, new XPoint(10, y));
        y += 12;
        if (!string.IsNullOrWhiteSpace(shipment.RecipientPhone))
        {
            gfx.DrawString(shipment.RecipientPhone, textFont, XBrushes.Black, new XPoint(10, y));
            y += 12;
        }
        gfx.DrawString(shipment.AddressStreet, textFont, XBrushes.Black, new XPoint(10, y));
        y += 12;
        var wardLine = string.IsNullOrWhiteSpace(shipment.AddressWard) ? string.Empty : $"{shipment.AddressWard}, ";
        gfx.DrawString($"{wardLine}{shipment.AddressDistrict}, {shipment.AddressProvince}", textFont, XBrushes.Black, new XPoint(10, y));
        y += 12;
        gfx.DrawString(shipment.AddressCountry, textFont, XBrushes.Black, new XPoint(10, y));
        y += 20;

        gfx.DrawLine(XPens.Black, 10, y, page.Width.Point - 10, y);
        y += 14;

        gfx.DrawString($"Order: {shipment.OrderId}", textFont, XBrushes.Black, new XPoint(10, y));
        y += 12;
        gfx.DrawString($"Weight: {shipment.TotalWeight:0.###} kg", textFont, XBrushes.Black, new XPoint(10, y));
        y += 12;
        if (shipment.EstimatedDelivery.HasValue)
            gfx.DrawString($"Est. delivery: {shipment.EstimatedDelivery.Value:yyyy-MM-dd}", textFont, XBrushes.Black, new XPoint(10, y));

        using var stream = new MemoryStream();
        document.Save(stream, closeStream: false);
        return stream.ToArray();
    }
}
