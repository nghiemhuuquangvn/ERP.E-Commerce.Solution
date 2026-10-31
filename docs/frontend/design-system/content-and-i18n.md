# Content and i18n

The words and number formats of the interface. Languages: English (`en`) and Vietnamese (`vi`) in all four apps; keys follow [../i18n-guide.md](../i18n-guide.md).

## 1. Voice
- **Plain, short, active.** Say what the user can do; no system jargon, no blame.
- Back-office: neutral and precise. Customer apps: friendly, second person, still short.
- One term per concept across the product (see glossary below). Vietnamese uses the formal "Quý khách" only in legal / payment text; elsewhere imperative verb forms ("Thêm vào giỏ hàng").
- Sentence case for buttons, headings, tabs, menu items ("Save promotion"); no trailing full stop on labels and buttons; no all-caps text typed in the string (use CSS where the style demands it).

## 2. Buttons and actions
Verb + object, specific: *Save*, *Add product*, *Approve order*, *Cancel promotion*, *Generate coupons*. Avoid *OK*, *Yes*, *Submit*. A destructive button repeats the action ("Void transaction", not "Confirm"). Cancel always means "do not do this" and is labelled *Cancel*; closing a read-only dialog is *Close*. Vietnamese: *Lưu*, *Thêm sản phẩm*, *Duyệt đơn hàng*, *Hủy khuyến mãi*, *Tạo mã giảm giá*, *Đóng*.

## 3. Messages
| Kind | Pattern | Example (en / vi) |
|---|---|---|
| Field error | what is wrong + how to fix, under the field | "Enter a valid email address." / "Nhập địa chỉ email hợp lệ." |
| Action error | what failed + what to do; include the server's business reason when it exists | "The promotion could not be saved. End date must be after start date." |
| Success | past tense, names the object, 4 s toast | "Promotion saved." / "Đã lưu khuyến mãi." |
| Confirmation | question naming the object + consequence | "Cancel “Spring sale”? This cannot be undone." |
| Empty | why it is empty + how to start | "No terminals yet. Add the first one to start selling." |
| Offline / outage | state + what still works | "You are offline. Saved data is shown." |
| Permission | what is missing, who can grant | "You don't have access to this page. Ask an administrator." |

Never show raw exception text, status codes or ids in a message; log them. HTTP 400 business-rule messages from the API are shown as the reason after our sentence. Placeholders are named (`{{name}}`), never concatenated.

## 4. Numbers, money, dates (presentation rules)
| Thing | Vietnamese (`vi`) | English (`en`) |
|---|---|---|
| Money (VND) | `1.234.567 ₫` (no decimals, symbol after, non-breaking space) | `₫1,234,567` |
| Other currency | code after the amount, 2 decimals: `12,50 USD` | `USD 12.50` |
| Integer / decimal | `1.234` / `12,5` | `1,234` / `12.5` |
| Percent | `12,5 %` (Back-office tables `12,5%`) | `12.5%` |
| Date | `28/10/2026` | `28 Oct 2026` |
| Date + time | `28/10/2026 14:05` (24 h) | `28 Oct 2026, 14:05` |
| Relative | "5 phút trước" up to 24 h, then date | "5 min ago" |
| Duration | `2 giờ 15 phút` | `2 h 15 min` |
| Phone | `0912 345 678` | same |
Rules: amounts right-aligned in tables with tabular digits; negative amounts with a minus and `danger` text only in accounting statements; a number input never loses the user's typed value on locale switch; UTC in the API, local time in the UI. The shop's currency, not the user's language, decides the currency symbol.
Implementation (follow-up U6): one shared `formatMoney / formatNumber / formatDate / formatDateTime / formatRelative` per platform reading the active i18n language; the 38+ local `money` helpers and `toLocale*` calls are then removed.

## 5. Glossary (same word everywhere)
| Concept | en | vi |
|---|---|---|
| Sales order | Sales order | Đơn bán hàng |
| Purchase order | Purchase order | Đơn mua hàng |
| Shipment / parcel | Shipment / parcel | Vận đơn / kiện hàng |
| Return | Return | Trả hàng |
| Coupon / promotion / flash sale | Coupon / promotion / flash sale | Mã giảm giá / khuyến mãi / flash sale |
| Cash on delivery | Cash on delivery (COD) | Thu hộ (COD) |
| Terminal / shift | POS terminal / cash shift | Máy POS / ca thu ngân |
| Void | Void | Hủy giao dịch |
| Tenant / shop | Shop | Cửa hàng |
Add a row when a new core term appears; reviewers reject a second word for the same thing.

## 6. Keys
`area.screen.element` (`promotion.promotions.addTitle`); shared words in `common.*` (`save`, `cancel`, `close`, `edit`, `delete`, `status`, `noRecordsFound`); statuses `<area>.status.<Name>`; enums `<area>.<enum>.<Name>`; plural forms with i18next plurals; never build a sentence from fragments; both language files change in the same commit; a key used in code must exist in both files (a unit test is part of U1).
