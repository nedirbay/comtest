using System.Text;
using ComTest.Application.Modules.Reporting.Contracts;
using ComTest.Infrastructure.Reporting;

namespace ComTest.UnitTests.Security;

public sealed class ReportFileTests
{
    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+99365111111", "'+99365111111")]
    [InlineData("-5", "'-5")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tx", "'\tx")]
    [InlineData("\rx", "'\rx")]
    [InlineData("Aýna", "Aýna")]
    [InlineData("", "")]
    public void TextThatASpreadsheetWouldRun_IsDefused(string text, string expected) =>
        Assert.Equal(expected, ReportFileWriter.DefuseFormula(text));

    [Fact]
    public void Csv_QuotesWhatNeedsQuotingAndLeavesNumbersAlone()
    {
        var table = new ReportTable(
            "test",
            ["Ady", "Mukdar"],
            [["=A,\"B\"", -5.5m], [null, 3]]);

        var file = new ReportFileWriter().Write(table, ReportFormat.Csv);
        var text = Encoding.UTF8.GetString(file.Content[3..]);

        Assert.Equal(Encoding.UTF8.GetPreamble(), file.Content[..3]);
        Assert.Contains("\"'=A,\"\"B\"\"\",-5.5", text, StringComparison.Ordinal);
        Assert.Contains(",3", text, StringComparison.Ordinal);
    }
}
