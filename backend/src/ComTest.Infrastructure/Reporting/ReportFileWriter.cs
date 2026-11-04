using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using ComTest.Application.Modules.Reporting.Contracts;

namespace ComTest.Infrastructure.Reporting;

/// <summary>
/// Files are built when asked for and never stored, so there is nothing left on a server for anyone
/// else to find later. Text in a cell comes from customers and staff, and a spreadsheet would run
/// it as a formula if it could, so <see cref="DefuseFormula"/> sits in front of every text cell.
/// </summary>
public sealed class ReportFileWriter : IReportFileWriter
{
    private const string CsvContentType = "text/csv; charset=utf-8";
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public ReportFile Write(ReportTable table, ReportFormat format)
    {
        ArgumentNullException.ThrowIfNull(table);

        var name = $"{table.Name}-{DateTime.UtcNow:yyyyMMdd-HHmm}";

        return format == ReportFormat.Xlsx
            ? new ReportFile(WriteXlsx(table), XlsxContentType, name + ".xlsx")
            : new ReportFile(WriteCsv(table), CsvContentType, name + ".csv");
    }

    /// <summary>A leading apostrophe makes a spreadsheet read the cell as text: `=1+1` stays `'=1+1`.</summary>
    public static string DefuseFormula(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
    }

    private static byte[] WriteCsv(ReportTable table)
    {
        var csv = new StringBuilder();

        csv.AppendLine(string.Join(',', table.Columns.Select(column => Quote(DefuseFormula(column)))));

        foreach (var row in table.Rows)
        {
            csv.AppendLine(string.Join(',', row.Select(ToCsvCell)));
        }

        // The mark tells Excel this is UTF-8, without which it garbles Turkmen and Russian letters.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];
    }

    private static string ToCsvCell(object? cell) => cell switch
    {
        null => string.Empty,
        string text => Quote(DefuseFormula(text)),
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable number => number.ToString(null, CultureInfo.InvariantCulture),
        _ => Quote(DefuseFormula(cell.ToString() ?? string.Empty)),
    };

    private static string Quote(string text) =>
        text.IndexOfAny(['"', ',', '\n', '\r']) >= 0 ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;

    private static byte[] WriteXlsx(ReportTable table)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet(table.Name);

        for (var column = 0; column < table.Columns.Count; column++)
        {
            var header = sheet.Cell(1, column + 1);

            header.Value = DefuseFormula(table.Columns[column]);
            header.Style.Font.Bold = true;
        }

        for (var row = 0; row < table.Rows.Count; row++)
        {
            for (var column = 0; column < table.Rows[row].Count; column++)
            {
                SetCell(sheet.Cell(row + 2, column + 1), table.Rows[row][column]);
            }
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();

        workbook.SaveAs(stream);

        return stream.ToArray();
    }

    private static void SetCell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string text:
                cell.Value = DefuseFormula(text);
                break;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "yyyy-mm-dd";
                break;
            case decimal amount:
                cell.Value = amount;
                break;
            case int count:
                cell.Value = count;
                break;
            case long count:
                cell.Value = count;
                break;
            default:
                cell.Value = DefuseFormula(value.ToString() ?? string.Empty);
                break;
        }
    }
}
