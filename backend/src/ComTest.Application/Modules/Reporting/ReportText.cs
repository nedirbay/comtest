using System.Globalization;

namespace ComTest.Application.Modules.Reporting;

/// <summary>Column titles of exported files, in the language of the person downloading them.</summary>
internal static class ReportText
{
    public static string Date => Pick("Sene", "Дата", "Date");

    public static string Orders => Pick("Sargyt", "Заказы", "Orders");

    public static string Units => Pick("Satylan sany", "Продано, шт.", "Units sold");

    public static string Product => Pick("Haryt", "Товар", "Product");

    public static string Sku => Pick("SKU", "Артикул", "SKU");

    public static string Customer => Pick("Müşderi", "Клиент", "Customer");

    public static string Phone => Pick("Telefon", "Телефон", "Phone");

    public static string OnHand => Pick("Ammarda", "На складе", "On hand");

    public static string ReservedUnits => Pick("Saklanan", "В резерве", "Reserved");

    public static string Available => Pick("Elýeterli", "Доступно", "Available");

    public static string Revenue(string currency) => Pick("Satuw", "Продажи", "Sales") + $" ({currency})";

    public static string Profit(string currency) => Pick("Takmynan gazanç", "Примерная прибыль", "Estimated profit") + $" ({currency})";

    public static string StockValue(string currency) => Pick("Ammaryň bahasy", "Стоимость запаса", "Stock value") + $" ({currency})";

    private static string Pick(string turkmen, string russian, string english) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName switch
        {
            "tk" => turkmen,
            "ru" => russian,
            _ => english,
        };
}
