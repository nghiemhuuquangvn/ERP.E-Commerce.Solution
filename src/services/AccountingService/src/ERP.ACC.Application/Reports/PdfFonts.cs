using System.Reflection;
using PdfSharp.Fonts;

namespace ERP.ACC.Application.Reports
{
    /// <summary>
    /// The font of the PDF exports (ROADMAP P1-2f). The runtime image has no fonts and Vietnamese needs one with its glyphs, so Noto Sans (SIL Open Font
    /// License, text in Reports/Fonts/OFL.txt) is embedded in the assembly and handed to PDFsharp through a font resolver.
    /// </summary>
    public sealed class PdfFonts : IFontResolver
    {
        public const string Family = "Noto Sans";
        private static readonly object Gate = new();
        private static bool _installed;

        /// <summary>Registers the resolver once per process; safe to call from every export.</summary>
        public static void Ensure()
        {
            if (_installed)
                return;
            lock (Gate)
            {
                if (_installed)
                    return;
                GlobalFontSettings.UseWindowsFontsUnderWindows = false;
                GlobalFontSettings.FontResolver = new PdfFonts();
                _installed = true;
            }
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // every family maps to Noto Sans: it is the only font the image carries, and MigraDoc asks for "Courier New" for its own error text
            return new FontResolverInfo(isBold ? "NotoSans-Bold" : "NotoSans-Regular");
        }

        public byte[]? GetFont(string faceName)
        {
            var resource = typeof(PdfFonts).Assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(faceName + ".ttf", StringComparison.Ordinal));
            if (resource == null)
                return null;
            using var stream = typeof(PdfFonts).Assembly.GetManifestResourceStream(resource)!;
            using var copy = new MemoryStream();
            stream.CopyTo(copy);
            return copy.ToArray();
        }
    }
}
