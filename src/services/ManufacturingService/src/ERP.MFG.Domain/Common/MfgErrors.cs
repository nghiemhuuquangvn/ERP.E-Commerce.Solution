using ERP.Shared.Infrastructure.Exceptions;

namespace ERP.MFG.Domain.Common;

/// <summary>
/// Every refusal of the Manufacturing service: code -> Vietnamese message (BA spec 01, Appendix A, BR-GEN-07).
/// The spec calls them PRD_*; the service code is MFG because PRD is ProductService, so the same
/// numbers are MFG_* here. Always refuse with <see cref="MfgRules.Fail"/> - it throws a
/// <see cref="BusinessRuleException"/> (HTTP 400) whose rule name is the code.
/// </summary>
public static class MfgErrors
{
    public const string Gen001 = "MFG_GEN_001";
    public const string Gen002 = "MFG_GEN_002";
    public const string Gen003 = "MFG_GEN_003";
    public const string Gen004 = "MFG_GEN_004";
    public const string Gen005 = "MFG_GEN_005";
    public const string Gen006 = "MFG_GEN_006";
    public const string Set001 = "MFG_SET_001";
    public const string Itm001 = "MFG_ITM_001";
    public const string Itm002 = "MFG_ITM_002";
    public const string Cal001 = "MFG_CAL_001";
    public const string Cal002 = "MFG_CAL_002";
    public const string Wc001 = "MFG_WC_001";
    public const string Wc002 = "MFG_WC_002";
    public const string Wc003 = "MFG_WC_003";
    public const string Wc004 = "MFG_WC_004";
    public const string Bom001 = "MFG_BOM_001";
    public const string Bom002 = "MFG_BOM_002";
    public const string Bom003 = "MFG_BOM_003";
    public const string Bom004 = "MFG_BOM_004";
    public const string Bom005 = "MFG_BOM_005";
    public const string Bom006 = "MFG_BOM_006";
    public const string Bom007 = "MFG_BOM_007";
    public const string Bom008 = "MFG_BOM_008";
    public const string Bom009 = "MFG_BOM_009";
    public const string Bom010 = "MFG_BOM_010";
    public const string Bom011 = "MFG_BOM_011";
    public const string Rt001 = "MFG_RT_001";
    public const string Rt002 = "MFG_RT_002";
    public const string Rt003 = "MFG_RT_003";
    public const string Rt004 = "MFG_RT_004";

    // Codes of the later phases (orders, materials, operations, receipts, quality, planning, costing)
    // are listed already so the catalogue is the single source; they are used from Phase 3 on.
    public const string Po001 = "MFG_PO_001";
    public const string Po002 = "MFG_PO_002";
    public const string Po003 = "MFG_PO_003";
    public const string Po004 = "MFG_PO_004";
    public const string Po005 = "MFG_PO_005";
    public const string Po006 = "MFG_PO_006";
    public const string Po007 = "MFG_PO_007";
    public const string Po008 = "MFG_PO_008";
    public const string Po009 = "MFG_PO_009";
    public const string Po010 = "MFG_PO_010";
    public const string Po011 = "MFG_PO_011";
    public const string Po012 = "MFG_PO_012";
    public const string Po013 = "MFG_PO_013";
    public const string Po014 = "MFG_PO_014";
    public const string Po015 = "MFG_PO_015";
    public const string Mat001 = "MFG_MAT_001";
    public const string Mat002 = "MFG_MAT_002";
    public const string Mat003 = "MFG_MAT_003";
    public const string Mat004 = "MFG_MAT_004";
    public const string Mat005 = "MFG_MAT_005";
    public const string Mat006 = "MFG_MAT_006";
    public const string Mat007 = "MFG_MAT_007";
    public const string Mat008 = "MFG_MAT_008";
    public const string Mat009 = "MFG_MAT_009";
    public const string Mat010 = "MFG_MAT_010";
    public const string Op001 = "MFG_OP_001";
    public const string Op002 = "MFG_OP_002";
    public const string Op003 = "MFG_OP_003";
    public const string Op004 = "MFG_OP_004";
    public const string Op005 = "MFG_OP_005";
    public const string Op006 = "MFG_OP_006";
    public const string Op007 = "MFG_OP_007";
    public const string Op008 = "MFG_OP_008";
    public const string Op009 = "MFG_OP_009";
    public const string Op010 = "MFG_OP_010";
    public const string Fg001 = "MFG_FG_001";
    public const string Fg002 = "MFG_FG_002";
    public const string Fg003 = "MFG_FG_003";
    public const string Fg004 = "MFG_FG_004";
    public const string Qc001 = "MFG_QC_001";
    public const string Qc002 = "MFG_QC_002";
    public const string Qc003 = "MFG_QC_003";
    public const string Ncr001 = "MFG_NCR_001";
    public const string Ncr002 = "MFG_NCR_002";
    public const string Mrp001 = "MFG_MRP_001";
    public const string Mrp002 = "MFG_MRP_002";
    public const string Cst002 = "MFG_CST_002";
    public const string Cst003 = "MFG_CST_003";

    // Not in the spec: needed because the module is split into services (BA spec §8 integrations).
    public const string Int001 = "MFG_INT_001";
    public const string Imp001 = "MFG_IMP_001";

    /// <summary>Code -> message template with {placeholders} (Appendix A of the BA spec).</summary>
    public static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<string, string>
    {
        [Gen001] = "Dữ liệu đã bị thay đổi bởi người dùng khác. Vui lòng tải lại và thử lại.",
        [Gen002] = "Bạn không có quyền thực hiện thao tác này.",
        [Gen003] = "Kỳ {period} đã khóa sổ, không thể ghi nhận giao dịch.",
        [Gen004] = "Ngày chứng từ không được lớn hơn ngày hiện tại.",
        [Gen005] = "Không thể xóa vì dữ liệu đang được sử dụng. Vui lòng chuyển sang ngừng hoạt động.",
        [Gen006] = "Mã {code} đã tồn tại.",
        [Set001] = "Không thể đổi phương pháp tính giá thành khi còn lệnh sản xuất chưa đóng.",
        [Itm001] = "Mặt hàng {item} không phải loại sản xuất (Make).",
        [Itm002] = "Tham số chính sách lô không hợp lệ.",
        [Cal001] = "Thời lượng ca làm việc không hợp lệ.",
        [Cal002] = "Ca {shift} bị chồng lấn thời gian với ca {other}.",
        [Wc001] = "Mã trung tâm công việc không đúng định dạng (2–20 ký tự A-Z, 0-9, '-').",
        [Wc002] = "Hiệu suất, tỷ lệ sử dụng hoặc số nguồn lực ngoài phạm vi cho phép.",
        [Wc003] = "Không thể ngừng hoạt động: còn công đoạn của lệnh sản xuất đang mở.",
        [Wc004] = "Trung tâm gia công ngoài phải chọn nhà cung cấp.",
        [Bom001] = "Mặt hàng cha của BOM phải là loại sản xuất (Make).",
        [Bom002] = "BOM đã phê duyệt hoặc hết hiệu lực không được chỉnh sửa.",
        [Bom003] = "Thời gian hiệu lực chồng lấn với phiên bản {version}.",
        [Bom004] = "Phát hiện vòng lặp BOM: {path}.",
        [Bom005] = "Số lượng, tỷ lệ hao hụt hoặc hiệu suất không hợp lệ.",
        [Bom006] = "Không có quy đổi đơn vị từ {from} sang {to} cho vật tư {item}.",
        [Bom007] = "Tổng tỷ lệ phân bổ chi phí đồng sản phẩm phải nhỏ hơn 100%.",
        [Bom008] = "Vượt quá số cấp BOM tối đa ({max}).",
        [Bom009] = "Nhóm thay thế {group} phải có đúng một vật tư ưu tiên 1.",
        [Bom010] = "Vật tư {item} bị trùng tại công đoạn {seq}.",
        [Bom011] = "Cụm phantom {item} chưa có BOM hiệu lực.",
        [Rt001] = "Số thứ tự công đoạn {seq} không hợp lệ hoặc bị trùng.",
        [Rt002] = "Thời gian setup/chạy hoặc cỡ lô không hợp lệ.",
        [Rt003] = "Trung tâm công việc không hoạt động hoặc không thuộc nhà máy.",
        [Rt004] = "Công đoạn gia công ngoài phải dùng trung tâm công việc loại gia công.",
        [Po001] = "Số lượng sản xuất phải lớn hơn 0.",
        [Po002] = "Không thể chuyển trạng thái lệnh từ {from} sang {to}.",
        [Po003] = "Ngày yêu cầu hoàn thành không được nhỏ hơn ngày hiện tại.",
        [Po004] = "Không tìm thấy BOM đã phê duyệt có hiệu lực tại ngày {date}.",
        [Po005] = "Phiên bản BOM/quy trình đã chọn không hiệu lực tại ngày bắt đầu.",
        [Po006] = "Mặt hàng, vật tư hoặc trung tâm công việc {code} đã ngừng hoạt động.",
        [Po007] = "Không đủ vật tư, không thể phát hành lệnh.",
        [Po008] = "Lệnh đã phát sinh giao dịch, không thể hủy hoặc hủy phát hành.",
        [Po009] = "Vui lòng chọn lý do.",
        [Po010] = "Số lượng mới không được nhỏ hơn số lượng đã hoàn thành ({completed}).",
        [Po011] = "Lệnh sản xuất đang tạm dừng.",
        [Po012] = "Số lượng hoàn thành dưới ngưỡng cho phép, cần quyền đóng thiếu và lý do.",
        [Po013] = "Chưa đủ điều kiện: {conditions}.",
        [Po014] = "Lệnh sản xuất đã đóng, không thể thay đổi.",
        [Po015] = "Vượt số lượng cho phép sản xuất ({max}).",
        [Mat001] = "Chỉ được xuất/trả vật tư cho lệnh ở trạng thái Đã phát hành hoặc Đang sản xuất.",
        [Mat002] = "Vượt định mức cho phép ({max}). Cần quyền xuất vượt định mức và lý do.",
        [Mat003] = "Tồn kho khả dụng không đủ (còn {available}).",
        [Mat004] = "Vật tư {item} quản lý theo lô, vui lòng chọn lô.",
        [Mat005] = "Lô {lot} đã hết hạn hoặc đang chờ kiểm.",
        [Mat006] = "Số lượng trả vượt số lượng đã xuất ({issued}).",
        [Mat007] = "Số lượng serial không khớp số lượng.",
        [Mat008] = "Không đủ tồn để trừ tự động vật tư {item} (thiếu {shortage}).",
        [Mat009] = "Vật tư thay thế không thuộc nhóm thay thế của định mức.",
        [Mat010] = "Số lượng yêu cầu vượt số lượng còn được cấp ({remaining}).",
        [Op001] = "Công đoạn chưa sẵn sàng hoặc lệnh đang tạm dừng.",
        [Op002] = "Số lượng báo cáo không hợp lệ.",
        [Op003] = "Vui lòng chọn lý do phế phẩm/làm lại.",
        [Op004] = "Vượt số lượng đầu vào khả dụng của công đoạn ({available}).",
        [Op005] = "Thời gian không hợp lệ.",
        [Op006] = "Nhân viên {employee} đang có phiên làm việc trùng thời gian.",
        [Op007] = "Không thể đảo: sản lượng đã được công đoạn sau hoặc nhập kho sử dụng.",
        [Op008] = "Thời gian dừng máy bị chồng lấn.",
        [Op009] = "Công đoạn đã hoàn thành.",
        [Op010] = "Máy {machine} đã ngừng sử dụng.",
        [Fg001] = "Vượt số lượng được phép nhập kho ({max}).",
        [Fg002] = "Số lô {lot} đã tồn tại.",
        [Fg003] = "Số serial không khớp hoặc bị trùng.",
        [Fg004] = "Không thể đảo phiếu nhập kho: {reason}.",
        [Qc001] = "Cỡ mẫu không đúng kế hoạch kiểm tra (yêu cầu {sample}).",
        [Qc002] = "Phiếu kiểm tra đã hoàn tất, không thể chỉnh sửa.",
        [Qc003] = "Chưa nhập đủ kết quả cho các tiêu chí kiểm tra.",
        [Ncr001] = "Tổng số lượng xử lý phải bằng số lượng không phù hợp ({qty}).",
        [Ncr002] = "Cần quyền phê duyệt sử dụng có điều kiện (Use-As-Is).",
        [Mrp001] = "MRP của nhà máy đang chạy, vui lòng đợi hoàn tất.",
        [Mrp002] = "Khoảng hoạch định phải từ 7 đến 365 ngày.",
        [Cst002] = "Phiên bản giá chuẩn đã phê duyệt không được chỉnh sửa.",
        [Cst003] = "Vật tư {item} chưa có giá chuẩn.",
        [Int001] = "Không lấy được dữ liệu từ dịch vụ {service}. Vui lòng thử lại sau.",
        [Imp001] = "Tệp nhập không hợp lệ: {reason}.",
    };

    /// <summary>The message of a code with its {placeholders} filled by <paramref name="args"/> (name, value pairs).</summary>
    public static string Format(string code, params (string Name, object? Value)[] args)
    {
        var text = Messages.TryGetValue(code, out var template) ? template : code;
        foreach (var (name, value) in args)
            text = text.Replace("{" + name + "}", Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "");
        return text;
    }
}

/// <summary>The one way to refuse a request in this service.</summary>
public static class MfgRules
{
    /// <summary>Throws a business-rule refusal (HTTP 400) whose rule name is the MFG_* code.</summary>
    public static BusinessRuleException Fail(string code, params (string Name, object? Value)[] args)
        => new(code, MfgErrors.Format(code, args));

    /// <summary>The generic refusal of an invalid value that has no code of its own in the spec (rule name <c>MFG_VALIDATION</c>, message in the caller's words).</summary>
    public static BusinessRuleException Invalid(string message) => new("MFG_VALIDATION", message);

    /// <summary>Throws <see cref="Fail"/> when <paramref name="violated"/> is true.</summary>
    public static void ThrowIf(bool violated, string code, params (string Name, object? Value)[] args)
    {
        if (violated) throw Fail(code, args);
    }
}
