namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// The notes B09-DN in Japanese, Chinese and French (ROADMAP P1-2l). Drafts like the rest of the notes: a translator or the accountant must review the wording before
    /// the notes are filed or shared. The Vietnamese form stays the one the Circular prescribes. PDF is available in French only (see <see cref="SupportsPdf"/>).
    /// </summary>
    public sealed partial class NoteStrings
    {
        public const string Japanese = "ja";
        public const string Chinese = "zh";
        public const string French = "fr";

        /// <summary>Every language the notes can be produced in.</summary>
        public static readonly string[] Supported = { Vietnamese, English, Japanese, Chinese, French };

        /// <summary>PDF needs a font with the glyphs: the embedded Noto Sans covers Latin scripts (vi, en, fr), not Japanese or Chinese.</summary>
        public static bool SupportsPdf(string language) => language is not (Japanese or Chinese);

        /// <summary>English, Japanese and Chinese write 1,234.56; Vietnamese and French write 1.234,56 / 1 234,56 (the PDF follows the Vietnamese form for both).</summary>
        public static bool UsesPointDecimal(string language) => language is English or Japanese or Chinese;

        public static readonly NoteStrings Ja = new()
        {
            Language = Japanese,
            Title = "財務諸表注記（様式B09-DN）— ドラフト",
            Draft = "ドラフト — 提出前に会計担当者による確認が必要です",
            CompanyName = "会社名", TaxCode = "税務コード", Address = "住所",
            PeriodSectionTitle = "会計期間および会計上の通貨",
            PeriodLine = "報告期間：{0}から{1}まで。", FiscalYearLine = "事業年度は{0}月末に終了します。",
            CurrencyLine = "会計上の通貨：ベトナム・ドン（VND）。", UnitLine = "報告書の単位：{0}。",
            UnitDong = "ドン", UnitThousand = "千ドン", UnitMillion = "百万ドン",
            Cash = "現金及び現金同等物", Receivables = "売上債権", Inventories = "棚卸資産",
            FixedAssets = "固定資産及び投資不動産（取得原価、減価償却累計額、帳簿価額）", ConstructionInProgress = "建設仮勘定",
            Prepaid = "前払費用", Payables = "仕入債務", Taxes = "税金及び国家への未払金", Accrued = "未払費用及びその他の未払金",
            Borrowings = "借入金及びファイナンス・リース債務（借入台帳より）", Equity = "資本（増減）", OffBalance = "貸借対照表外項目",
            Revenue = "収益及び収入", Expenses = "売上原価及び費用", NonCashTitle = "非資金取引（仕訳帳より）",
            Total = "合計", OfWhichLongTerm = "うち：12か月超に期限到来（長期）", InventoryCostTotal = "棚卸資産原価合計", InventoryProvision = "棚卸資産評価引当金",
            Cost = "取得原価", AccumulatedDepreciation = "減価償却累計額", NetBookValue = "帳簿価額",
            RevenueTotal = "収益及び収入合計", ExpensesTotal = "売上原価及び費用合計", LedgerBalance341 = "元帳残高（勘定341）",
            NonCashAssetsOnCredit = "債務による固定資産の取得", NonCashDebtToEquity = "債務の資本への振替",
            Opening = "期首", Increase = "増加", Decrease = "減少", Closing = "期末", Amount = "金額", PriorClosing = "前期末", PriorAmount = "前期",
            LoanPrincipal = "元本", LoanOutstanding = "期末残高", LoanWithin12 = "12か月以内に期限到来", LoanAfter12 = "12か月超に期限到来",
            PdfPage = "ページ ", PdfTaxCode = "税務コード：", PdfAddress = "住所：", PdfDraft = "ドラフト — 提出前に会計担当者による確認が必要です",
            PdfPeriod = "{0}から{1}まで — 単位：{2}", PdfNotWritten = "（未記入 — {0}）", PdfNoData = "（当期の取引なし）", PdfNotePrefix = "注：",
            PdfItem = "項目", PdfPreparedBy = "作成者", PdfChiefAccountant = "経理責任者", PdfDirector = "代表者",
        };

        public static readonly NoteStrings Zh = new()
        {
            Language = Chinese,
            Title = "财务报表附注（表B09-DN）— 草稿",
            Draft = "草稿 — 提交前须经会计人员复核",
            CompanyName = "企业名称", TaxCode = "税号", Address = "地址",
            PeriodSectionTitle = "会计期间及记账本位币",
            PeriodLine = "报告期间：自{0}至{1}。", FiscalYearLine = "会计年度于{0}月末结束。",
            CurrencyLine = "记账本位币：越南盾（VND）。", UnitLine = "报表单位：{0}。",
            UnitDong = "越南盾", UnitThousand = "千越南盾", UnitMillion = "百万越南盾",
            Cash = "货币资金及现金等价物", Receivables = "应收账款", Inventories = "存货",
            FixedAssets = "固定资产及投资性房地产（原值、累计折旧、账面价值）", ConstructionInProgress = "在建工程",
            Prepaid = "预付费用", Payables = "应付账款", Taxes = "应交税费及应付国家款项", Accrued = "预提费用及其他应付款",
            Borrowings = "借款及融资租赁负债（来自借款台账）", Equity = "所有者权益（变动）", OffBalance = "表外项目",
            Revenue = "收入及收益", Expenses = "销售成本及费用", NonCashTitle = "非现金交易（来自日记账）",
            Total = "合计", OfWhichLongTerm = "其中：12个月后到期（长期）", InventoryCostTotal = "存货成本合计", InventoryProvision = "存货跌价准备",
            Cost = "原值", AccumulatedDepreciation = "累计折旧", NetBookValue = "账面价值",
            RevenueTotal = "收入及收益合计", ExpensesTotal = "销售成本及费用合计", LedgerBalance341 = "总账余额（账户341）",
            NonCashAssetsOnCredit = "以负债方式购置固定资产", NonCashDebtToEquity = "债务转为权益",
            Opening = "期初", Increase = "增加", Decrease = "减少", Closing = "期末", Amount = "金额", PriorClosing = "上期期末", PriorAmount = "上期",
            LoanPrincipal = "本金", LoanOutstanding = "期末余额", LoanWithin12 = "12个月内到期", LoanAfter12 = "12个月后到期",
            PdfPage = "第 ", PdfTaxCode = "税号：", PdfAddress = "地址：", PdfDraft = "草稿 — 提交前须经会计人员复核",
            PdfPeriod = "自{0}至{1} — 单位：{2}", PdfNotWritten = "（尚未填写 — {0}）", PdfNoData = "（本期无发生额）", PdfNotePrefix = "注：",
            PdfItem = "项目", PdfPreparedBy = "制表人", PdfChiefAccountant = "总会计师", PdfDirector = "负责人",
        };

        public static readonly NoteStrings Fr = new()
        {
            Language = French,
            Title = "Annexe aux états financiers (modèle B09-DN) — projet",
            Draft = "Projet — à vérifier par le comptable avant dépôt",
            CompanyName = "Raison sociale", TaxCode = "Numéro fiscal", Address = "Adresse",
            PeriodSectionTitle = "Période comptable et monnaie de tenue des comptes",
            PeriodLine = "Période : du {0} au {1}.", FiscalYearLine = "L'exercice se termine à la fin du mois {0}.",
            CurrencyLine = "Monnaie de tenue des comptes : dong vietnamien (VND).", UnitLine = "Unité du rapport : {0}.",
            UnitDong = "dong", UnitThousand = "milliers de dongs", UnitMillion = "millions de dongs",
            Cash = "Trésorerie et équivalents de trésorerie", Receivables = "Créances clients", Inventories = "Stocks",
            FixedAssets = "Immobilisations et immeubles de placement (coût, amortissements, valeur nette comptable)", ConstructionInProgress = "Immobilisations en cours",
            Prepaid = "Charges constatées d'avance", Payables = "Dettes fournisseurs", Taxes = "Impôts, taxes et sommes dues à l'État", Accrued = "Charges à payer et autres dettes",
            Borrowings = "Emprunts et dettes de crédit-bail (d'après le registre des emprunts)", Equity = "Capitaux propres (variation)", OffBalance = "Éléments hors bilan",
            Revenue = "Produits", Expenses = "Coût des ventes et charges", NonCashTitle = "Opérations sans flux de trésorerie (d'après le journal)",
            Total = "Total", OfWhichLongTerm = "Dont : échéance à plus de 12 mois (long terme)", InventoryCostTotal = "Total du coût des stocks", InventoryProvision = "Dépréciation des stocks",
            Cost = "Coût", AccumulatedDepreciation = "Amortissements cumulés", NetBookValue = "Valeur nette comptable",
            RevenueTotal = "Total des produits", ExpensesTotal = "Total du coût des ventes et des charges", LedgerBalance341 = "Solde du grand livre (compte 341)",
            NonCashAssetsOnCredit = "Immobilisations acquises à crédit", NonCashDebtToEquity = "Dette convertie en capitaux propres",
            Opening = "Ouverture", Increase = "Augmentation", Decrease = "Diminution", Closing = "Clôture", Amount = "Montant", PriorClosing = "Clôture précédente", PriorAmount = "Période précédente",
            LoanPrincipal = "Principal", LoanOutstanding = "Encours en fin de période", LoanWithin12 = "Échéance dans les 12 mois", LoanAfter12 = "Échéance après 12 mois",
            PdfPage = "Page ", PdfTaxCode = "Numéro fiscal : ", PdfAddress = "Adresse : ", PdfDraft = "Projet — à vérifier par le comptable avant dépôt",
            PdfPeriod = "Du {0} au {1} — Unité : {2}", PdfNotWritten = "(Non rédigé — {0})", PdfNoData = "(Aucun mouvement sur la période)", PdfNotePrefix = "Note : ",
            PdfItem = "Rubrique", PdfPreparedBy = "Établi par", PdfChiefAccountant = "Chef comptable", PdfDirector = "Directeur",
        };
    }

    /// <summary>The titles and hints of the note sections in Japanese, Chinese and French (Vietnamese and English live on <see cref="NoteTextSections.Section"/>).</summary>
    public static class NoteSectionTranslations
    {
        public static (string Title, string Hint)? Find(string key, string language) =>
            All.TryGetValue((key, language), out var t) ? t : null;

        public static readonly IReadOnlyDictionary<(string Key, string Language), (string Title, string Hint)> All = new Dictionary<(string, string), (string, string)>
        {
            [("general", "ja")] = ("事業の特徴", "資本の所有形態、事業分野、事業内容、事業サイクル、企業グループの構造。"),
            [("general", "zh")] = ("企业经营特点", "资本所有制形式、经营领域、行业、经营周期、企业架构。"),
            [("general", "fr")] = ("Nature des activités", "Forme de propriété du capital, domaine d'activité, cycle d'exploitation, structure du groupe."),

            [("standards", "ja")] = ("適用する会計基準及び会計制度", "適用する会計制度（通達99/2025/TT-BTC又は200/2014/TT-BTC）及び会計基準への準拠表明。"),
            [("standards", "zh")] = ("适用的会计准则和会计制度", "适用的会计制度（第99/2025/TT-BTC号通知或第200/2014/TT-BTC号通知）及遵循准则的声明。"),
            [("standards", "fr")] = ("Normes et régime comptables appliqués", "Le régime appliqué (circulaire 99/2025/TT-BTC ou 200/2014/TT-BTC) et la déclaration de conformité aux normes."),

            [("policy.cash", "ja")] = ("会計方針：現金及び現金同等物", "現金及び現金同等物の認識、外貨換算。"),
            [("policy.cash", "zh")] = ("会计政策：货币资金及现金等价物", "货币资金及现金等价物的确认；外币折算。"),
            [("policy.cash", "fr")] = ("Méthode comptable : trésorerie et équivalents", "Comptabilisation de la trésorerie et des équivalents de trésorerie ; conversion des devises."),

            [("policy.receivables", "ja")] = ("会計方針：債権", "認識、短期・長期の区分、貸倒引当金の設定。"),
            [("policy.receivables", "zh")] = ("会计政策：应收款项", "确认、短期与长期的分类、坏账准备的计提。"),
            [("policy.receivables", "fr")] = ("Méthode comptable : créances", "Comptabilisation, classement court et long terme, provision pour créances douteuses."),

            [("policy.inventory", "ja")] = ("会計方針：棚卸資産", "棚卸資産の評価、原価計算方法（平均法、先入先出法）、評価損の引当。"),
            [("policy.inventory", "zh")] = ("会计政策：存货", "存货计价、发出存货成本计算方法（平均法、先进先出法）、跌价准备。"),
            [("policy.inventory", "fr")] = ("Méthode comptable : stocks", "Évaluation des stocks, méthode de coût (moyenne, PEPS), provision pour dépréciation."),

            [("policy.fixedassets", "ja")] = ("会計方針：固定資産及び減価償却", "取得原価の認識、減価償却方法、耐用年数。"),
            [("policy.fixedassets", "zh")] = ("会计政策：固定资产及折旧", "原值的确认、折旧方法、使用年限。"),
            [("policy.fixedassets", "fr")] = ("Méthode comptable : immobilisations et amortissements", "Comptabilisation du coût, méthode d'amortissement, durées d'utilité."),

            [("policy.provisions", "ja")] = ("会計方針：引当金", "債務に対する引当金の認識。"),
            [("policy.provisions", "zh")] = ("会计政策：预计负债", "预计负债的确认。"),
            [("policy.provisions", "fr")] = ("Méthode comptable : provisions", "Comptabilisation des provisions pour risques et charges."),

            [("policy.revenue", "ja")] = ("会計方針：収益及び費用の認識", "商品販売、役務提供、金融収益の収益認識要件、費用の認識。"),
            [("policy.revenue", "zh")] = ("会计政策：收入和费用的确认", "销售商品、提供劳务、财务收入的确认条件；费用的确认。"),
            [("policy.revenue", "fr")] = ("Méthode comptable : comptabilisation des produits et des charges", "Conditions de comptabilisation des produits (ventes, services, produits financiers) ; comptabilisation des charges."),

            [("policy.tax", "ja")] = ("会計方針：税金", "法人所得税率、付加価値税及びその他の税金、繰延税金。"),
            [("policy.tax", "zh")] = ("会计政策：税项", "企业所得税税率、增值税及其他税种；递延所得税。"),
            [("policy.tax", "fr")] = ("Méthode comptable : impôts et taxes", "Taux de l'impôt sur les sociétés, TVA et autres taxes ; impôts différés."),

            [("policy.fx", "ja")] = ("会計方針：外貨", "使用する為替レート及び為替差損益の処理。"),
            [("policy.fx", "zh")] = ("会计政策：外币", "采用的汇率及汇兑差额的处理。"),
            [("policy.fx", "fr")] = ("Méthode comptable : devises", "Taux de change utilisés et traitement des écarts de change."),

            [("noncash", "ja")] = ("キャッシュ・フロー計算書の補足情報", "キャッシュ・フロー計算書に影響する非資金取引（債務による資産取得、債務の資本への振替など）。"),
            [("noncash", "zh")] = ("现金流量表补充信息", "影响现金流量表的非现金交易（以负债购置资产、债务转为权益等）。"),
            [("noncash", "fr")] = ("Informations complémentaires sur le tableau des flux de trésorerie", "Opérations sans flux de trésorerie qui touchent le tableau (actifs achetés à crédit, dette convertie en capitaux propres …)."),

            [("subsequent", "ja")] = ("会計期間終了後の事象", "報告日後に生じた重要な事象。"),
            [("subsequent", "zh")] = ("会计期间结束后发生的事项", "报告日后发生的重大事项。"),
            [("subsequent", "fr")] = ("Événements postérieurs à la clôture", "Événements importants survenus après la date de clôture."),

            [("other", "ja")] = ("その他の情報", "その他の開示事項（関連当事者、偶発債務、コミットメントなど）。"),
            [("other", "zh")] = ("其他信息", "其他需披露的信息（关联方、或有负债、承诺等）。"),
            [("other", "fr")] = ("Autres informations", "Autres informations à fournir (parties liées, passifs éventuels, engagements …)."),
        };
    }
}
