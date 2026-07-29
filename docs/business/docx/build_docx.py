"""
Markdown -> .docx converter for the Senior BA documentation set.

Handles a constrained Markdown subset used across this repo's docs/business
and docs/architecture files: headings (# .. ####), tables, bullet/numbered
lists, blockquotes, fenced code blocks, horizontal-rule page breaks, and
inline bold/italic/code spans. Not a general-purpose Markdown parser --
just enough to faithfully render this repo's own writing conventions.

Usage:
    python build_docx.py

Regenerates all 6 deliverables into docs/business/docx/*.docx from the
markdown sources listed in DOCUMENTS below.
"""

import re
from pathlib import Path
from datetime import date

from docx import Document
from docx.shared import Pt, Cm, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT
from docx.oxml.ns import qn
from docx.oxml import OxmlElement

REPO_ROOT = Path(__file__).resolve().parents[3]
OUT_DIR = Path(__file__).resolve().parent

HEADER_SHADING = "D9E2F3"
INLINE_CODE_SHADING = "F2F2F2"


def set_cell_background(cell, color_hex):
    shd = OxmlElement("w:shd")
    shd.set(qn("w:fill"), color_hex)
    cell._tc.get_or_add_tcPr().append(shd)


def add_page_break(doc):
    doc.add_page_break()


def add_title_page(doc, title, subtitle):
    p = doc.add_paragraph()
    p.alignment = WD_ALIGN_PARAGRAPH.CENTER
    for _ in range(6):
        doc.add_paragraph()
    run = p.add_run(title)
    run.bold = True
    run.font.size = Pt(28)

    p2 = doc.add_paragraph()
    p2.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run2 = p2.add_run(subtitle)
    run2.font.size = Pt(14)
    run2.font.color.rgb = RGBColor(0x44, 0x44, 0x44)

    p3 = doc.add_paragraph()
    p3.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run3 = p3.add_run("Senior BA Documentation Set")
    run3.italic = True
    run3.font.size = Pt(12)

    p4 = doc.add_paragraph()
    p4.alignment = WD_ALIGN_PARAGRAPH.CENTER
    run4 = p4.add_run(date.today().isoformat())
    run4.font.size = Pt(11)
    run4.font.color.rgb = RGBColor(0x66, 0x66, 0x66)

    add_page_break(doc)


def add_toc_field(doc):
    doc.add_heading("Table of Contents", level=1)
    paragraph = doc.add_paragraph()
    run = paragraph.add_run()
    fld_char1 = OxmlElement("w:fldChar")
    fld_char1.set(qn("w:fldCharType"), "begin")
    instr_text = OxmlElement("w:instrText")
    instr_text.set(qn("xml:space"), "preserve")
    instr_text.text = 'TOC \\o "1-3" \\h \\z \\u'
    fld_char2 = OxmlElement("w:fldChar")
    fld_char2.set(qn("w:fldCharType"), "separate")
    fld_char3 = OxmlElement("w:t")
    fld_char3.text = "Right-click and choose 'Update Field' to build the table of contents."
    fld_char4 = OxmlElement("w:fldChar")
    fld_char4.set(qn("w:fldCharType"), "end")
    run._r.append(fld_char1)
    run._r.append(instr_text)
    run._r.append(fld_char2)
    run._r.append(fld_char3)
    run._r.append(fld_char4)
    add_page_break(doc)


INLINE_PATTERN = re.compile(r"(\*\*.+?\*\*|`.+?`|\*[^*]+?\*)")


def add_inline_runs(paragraph, text):
    """Split a line on **bold**, `code`, *italic* and add styled runs."""
    parts = INLINE_PATTERN.split(text)
    for part in parts:
        if not part:
            continue
        if part.startswith("**") and part.endswith("**") and len(part) > 4:
            run = paragraph.add_run(part[2:-2])
            run.bold = True
        elif part.startswith("`") and part.endswith("`") and len(part) > 2:
            run = paragraph.add_run(part[1:-1])
            run.font.name = "Consolas"
            run.font.size = Pt(9.5)
        elif part.startswith("*") and part.endswith("*") and len(part) > 2:
            run = paragraph.add_run(part[1:-1])
            run.italic = True
        else:
            paragraph.add_run(part)


def parse_table_block(lines, start_idx):
    """Parse a contiguous block of |...| lines starting at start_idx. Returns (rows, next_idx)."""
    rows = []
    i = start_idx
    while i < len(lines) and lines[i].strip().startswith("|"):
        line = lines[i].strip()
        if re.match(r"^\|[\s\-:|]+\|$", line):
            i += 1
            continue
        cells = [c.strip() for c in line.strip("|").split("|")]
        rows.append(cells)
        i += 1
    return rows, i


def add_table(doc, rows):
    if not rows:
        return
    ncols = max(len(r) for r in rows)
    table = doc.add_table(rows=len(rows), cols=ncols)
    table.style = "Table Grid"
    table.alignment = WD_TABLE_ALIGNMENT.CENTER
    for r_idx, row in enumerate(rows):
        for c_idx in range(ncols):
            text = row[c_idx] if c_idx < len(row) else ""
            cell = table.cell(r_idx, c_idx)
            cell.text = ""
            p = cell.paragraphs[0]
            add_inline_runs(p, text)
            for run in p.runs:
                run.font.size = Pt(9.5)
            if r_idx == 0:
                set_cell_background(cell, HEADER_SHADING)
                for run in p.runs:
                    run.bold = True
    doc.add_paragraph()


def render_markdown(doc, md_text, base_heading_level=1):
    """Render one markdown document's body into doc, offsetting heading levels
    so that within a combined multi-source docx, a source's '# Title' becomes
    a level-(base_heading_level) heading instead of colliding with the
    document title itself."""
    lines = md_text.splitlines()
    i = 0
    in_code_block = False
    code_lines = []

    while i < len(lines):
        line = lines[i]
        stripped = line.strip()

        if stripped.startswith("```"):
            if not in_code_block:
                in_code_block = True
                code_lines = []
            else:
                in_code_block = False
                p = doc.add_paragraph()
                p.paragraph_format.left_indent = Cm(0.5)
                code_text = "\n".join(code_lines)
                run = p.add_run(code_text)
                run.font.name = "Consolas"
                run.font.size = Pt(8.5)
                shd = OxmlElement("w:shd")
                shd.set(qn("w:fill"), INLINE_CODE_SHADING)
                p.paragraph_format.element.get_or_add_pPr().append(shd)
            i += 1
            continue

        if in_code_block:
            code_lines.append(line)
            i += 1
            continue

        if not stripped:
            i += 1
            continue

        if stripped == "---":
            i += 1
            continue

        if stripped.startswith("| "):
            rows, i = parse_table_block(lines, i)
            add_table(doc, rows)
            continue

        heading_match = re.match(r"^(#{1,4})\s+(.*)$", stripped)
        if heading_match:
            hashes, text = heading_match.groups()
            level = min(base_heading_level + len(hashes) - 1, 4)
            h = doc.add_heading(level=level)
            add_inline_runs(h, text)
            i += 1
            continue

        if stripped.startswith("> "):
            p = doc.add_paragraph()
            p.paragraph_format.left_indent = Cm(0.8)
            add_inline_runs(p, stripped[2:])
            for run in p.runs:
                run.italic = True
                run.font.color.rgb = RGBColor(0x55, 0x55, 0x55)
            i += 1
            continue

        list_match = re.match(r"^(\s*)([-*]|\d+\.)\s+(.*)$", line)
        if list_match:
            indent, marker, text = list_match.groups()
            style = "List Bullet" if marker in ("-", "*") else "List Number"
            p = doc.add_paragraph(style=style)
            depth = len(indent) // 2
            p.paragraph_format.left_indent = Cm(0.6 + depth * 0.5)
            add_inline_runs(p, text)
            i += 1
            continue

        p = doc.add_paragraph()
        add_inline_runs(p, stripped)
        i += 1


def read(path_str):
    path = REPO_ROOT / path_str
    return path.read_text(encoding="utf-8")


def build_document(filename, title, subtitle, sources):
    """sources: list of (path_str, section_label) tuples."""
    doc = Document()

    style = doc.styles["Normal"]
    style.font.name = "Calibri"
    style.font.size = Pt(10.5)

    for section in doc.sections:
        section.left_margin = Cm(2.2)
        section.right_margin = Cm(2.2)
        section.top_margin = Cm(2.0)
        section.bottom_margin = Cm(2.0)

    add_title_page(doc, title, subtitle)
    add_toc_field(doc)

    for path_str, section_label in sources:
        doc.add_heading(section_label, level=1)
        text = read(path_str)
        render_markdown(doc, text, base_heading_level=2)
        add_page_break(doc)

    out_path = OUT_DIR / filename
    doc.save(str(out_path))
    print(f"Wrote {out_path} ({out_path.stat().st_size:,} bytes)")


DOCUMENTS = [
    dict(
        filename="00-ERP-Master-Overview.docx",
        title="ERP E-Commerce Solution",
        subtitle="Master Overview — Domains, Services, Architecture",
        sources=[
            ("docs/business/README.md", "Business Domains Index"),
            ("docs/architecture/domain-map.md", "Domain Map"),
            ("docs/architecture/service-map.md", "Service Map"),
        ],
    ),
    dict(
        filename="01-ERP-Core-BackOffice.docx",
        title="ERP Core — Back-Office Modules",
        subtitle="Identity · Accounting/Finance · HRM · Purchasing",
        sources=[
            ("docs/business/Identity/module-02-identity-security.md", "Module 2 — Identity & Security"),
            ("docs/business/T/module-12-finance.md", "Module 12 — Finance / Accounting"),
            ("docs/business/HRM/BA-Analysis/00-domain-overview.md", "HRM — Domain Overview"),
            ("docs/business/HRM/BA-Analysis/01-employee-profile.md", "HRM — Employee Profile"),
            ("docs/business/HRM/BA-Analysis/02-recruitment.md", "HRM — Recruitment"),
            ("docs/business/HRM/BA-Analysis/03-labor-contract.md", "HRM — Labor Contract"),
            ("docs/business/HRM/BA-Analysis/04-timekeeping-shift.md", "HRM — Timekeeping & Shift"),
            ("docs/business/HRM/BA-Analysis/05-request-approval.md", "HRM — Request & Approval"),
            ("docs/business/HRM/BA-Analysis/06-payroll.md", "HRM — Payroll"),
            ("docs/business/HRM/BA-Analysis/07-social-insurance.md", "HRM — Social Insurance"),
            ("docs/business/HRM/BA-Analysis/08-personal-income-tax.md", "HRM — Personal Income Tax"),
            ("docs/business/HRM/BA-Analysis/09-training.md", "HRM — Training"),
            ("docs/business/HRM/BA-Analysis/10-maternity-benefits.md", "HRM — Maternity Benefits"),
            ("docs/business/HRM/BA-Analysis/11-personnel-decisions.md", "HRM — Personnel Decisions"),
            ("docs/business/Purchasing/PurchaseOrder.md", "Purchasing — Purchase Order"),
            ("docs/business/Purchasing/PurchasingWorkflow.md", "Purchasing — Workflow"),
            ("docs/business/Purchasing/Supplier.md", "Purchasing — Supplier"),
        ],
    ),
    dict(
        filename="02-Supply-Chain-Product.docx",
        title="Supply Chain & Product Management",
        subtitle="Product · Warehouse · Inventory · Master Data",
        sources=[
            ("docs/business/Product/module-09-product.md", "Module 9 — Product"),
            ("docs/business/Warehouse/module-07-warehouse.md", "Module 7 — Warehouse"),
            ("docs/business/Inventory/module-08-inventory.md", "Module 8 — Inventory"),
            ("docs/business/MasterData/module-01-organization-service.md", "Module 1 — Organization Service"),
            ("docs/business/MasterData/module-03-master-data.md", "Module 3 — Master Data"),
        ],
    ),
    dict(
        filename="03-Ecommerce-Sales.docx",
        title="E-Commerce & Sales",
        subtitle="Sales · CRM · Payment · Promotion · Shipping",
        sources=[
            ("docs/business/Sales/module-05-sales.md", "Module 5 — Sales"),
            ("docs/business/CRM/module-10-crm.md", "Module 10 — CRM"),
            ("docs/business/Payment/module-11-payment.md", "Module 11 — Payment"),
            ("docs/business/Promotion/module-20-promotion.md", "Module 20 — Promotion"),
            ("docs/business/Shipping/module-13-shipping.md", "Module 13 — Shipping"),
        ],
    ),
    dict(
        filename="04-Retail-CrossCutting.docx",
        title="Retail & Cross-Cutting Concerns",
        subtitle="POS · Notification · Reporting",
        sources=[
            ("docs/business/POS/module-18-ecommerce-pos.md", "Module 18 — POS / E-Commerce"),
            ("docs/business/Notification/module-21-notification.md", "Module 21 — Notification"),
            ("docs/business/Reporting/module-19-reporting.md", "Module 19 — Reporting"),
        ],
    ),
    dict(
        filename="05-Database-Performance-Indexing-Review.docx",
        title="Database Performance & Indexing Review",
        subtitle="B-Tree Correctness Across All 16 Databases",
        sources=[
            ("docs/architecture/database-performance-indexing-review.md", "Full Review"),
        ],
    ),
]


def main():
    for spec in DOCUMENTS:
        build_document(**spec)


if __name__ == "__main__":
    main()
