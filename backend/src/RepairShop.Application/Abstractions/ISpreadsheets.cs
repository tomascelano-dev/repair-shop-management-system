namespace RepairShop.Application.Abstractions;

public sealed record SheetData(string Name, IReadOnlyList<string> Headers, IReadOnlyList<object?[]> Rows);

/// <summary>Excel (.xlsx) generation.</summary>
public interface IExcelExporter
{
    byte[] Export(IReadOnlyList<SheetData> sheets);
}

/// <summary>Reads tabular files (.xlsx / .csv) as rows of header -> value.</summary>
public interface ISpreadsheetReader
{
    IReadOnlyList<IReadOnlyDictionary<string, string>> Read(Stream content, string fileName);
}
