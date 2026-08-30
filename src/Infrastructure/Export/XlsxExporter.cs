using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace PlanoOpenSpaceIT.Windows;

internal static class XlsxExporter
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Xml = "http://www.w3.org/XML/1998/namespace";

    public static XlsxExportResult Write(JsonObject loaded, string outputPath)
    {
        CopyTemplate(outputPath);
        using var archive = ZipFile.Open(outputPath, ZipArchiveMode.Update);
        var strings = LoadSharedStrings(archive);
        var plan = PlanOccupancy(loaded);
        var sheet1 = LoadWorksheet(archive, "xl/worksheets/sheet1.xml");
        var sheet2 = LoadWorksheet(archive, "xl/worksheets/sheet2.xml");
        var sheet3 = LoadWorksheet(archive, "xl/worksheets/sheet3.xml");
        var master = TemplateEntries(sheet2, "A", ["B", "C"], strings);
        var templateRosetas = master.Where(entry => entry.Roseta is not null).Select(entry => entry.Roseta!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var planRosetas = plan.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var planOnly = plan.Keys.Where(roseta => !templateRosetas.Contains(roseta)).OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray();
        var duplicates = plan.Where(pair => pair.Value.Count > 1).OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase).ToArray();
        if (duplicates.Length > 0) throw new InvalidDataException("No se puede exportar: hay rosetas duplicadas en el plano. " + string.Join(", ", duplicates.Select(pair => pair.Key + " (" + string.Join(", ", pair.Value.Select(item => item.WorkstationId).Order()) + ")")));

        var one = RenderSheet(sheet1, "G", ["H", "I", "J"], null, TemplateEntries(sheet1, "G", ["A", "B", "C", "D", "E", "F"], strings), plan);
        var two = RenderSheet(sheet2, "A", ["D", "E", "F", "G", "H", "I"], WriteRosetasOccupancy, master, plan);
        var three = RenderSheet(sheet3, "H", ["I", "J", "K", "L", "M"], WriteParcheoOccupancy, TemplateEntries(sheet3, "H", ["A", "B", "C", "D", "E", "F", "G"], strings), plan);

        SaveWorksheet(archive, "xl/worksheets/sheet1.xml", sheet1);
        SaveWorksheet(archive, "xl/worksheets/sheet2.xml", sheet2);
        SaveWorksheet(archive, "xl/worksheets/sheet3.xml", sheet3);
        return new XlsxExportResult(two.Rows, templateRosetas.Count, planRosetas.Count, templateRosetas.Intersect(planRosetas, StringComparer.OrdinalIgnoreCase).Count(), planOnly.Length, two.Rows, 0, master.Count(entry => entry.Roseta is null && entry.HasInfrastructure), one.SkippedInvalid + two.SkippedInvalid + three.SkippedInvalid);
    }

    private static Dictionary<string, List<Occupancy>> PlanOccupancy(JsonObject loaded)
    {
        var people = Index(loaded["people"]?["people"]?.AsArray(), "id");
        var devices = Index(loaded["devices"]?["devices"]?.AsArray(), "id");
        var locations = Index(loaded["locations"]?["locations"]?.AsArray(), "id");
        var seats = loaded["maps"]?["maps"]?.AsArray().OfType<JsonObject>().SelectMany(map => map["seats"]?.AsArray().OfType<JsonObject>() ?? []).Select(seat => Text(seat["id"])).ToHashSet(StringComparer.Ordinal) ?? [];
        var result = new Dictionary<string, List<Occupancy>>(StringComparer.OrdinalIgnoreCase);
        foreach (var assignment in loaded["assignments"]?["assignments"]?.AsArray().OfType<JsonObject>() ?? [])
        {
            var seatId = Text(assignment["workstationId"]);
            var roseta = NormalizeRoseta(Text(assignment["roseta"]));
            if (!seats.Contains(seatId) || roseta is null) continue;
            var item = new Occupancy(seatId, roseta, Lookup(people, Text(assignment["personId"]), "username", "name"), Lookup(devices, Text(assignment["deviceId"]), "name", "serialNumber"), Lookup(locations, Text(assignment["locationId"]), "name"), DateForExcel(Text(assignment["updatedAt"])));
            if (!result.TryGetValue(roseta, out var list)) result[roseta] = list = [];
            list.Add(item);
        }
        return result;
    }

    private static RenderResult RenderSheet(XDocument document, string rosetaColumn, string[] occupationColumns, Action<XElement, Occupancy>? writeOccupancy, IReadOnlyList<TemplateEntry> entries, IReadOnlyDictionary<string, List<Occupancy>> plan)
    {
        foreach (var entry in entries)
        {
            foreach (var column in occupationColumns) Clear(entry.Row, column);
            if (entry.InvalidRoseta) { SetText(entry.Row, rosetaColumn, ""); continue; }
            if (entry.Roseta is not null && plan.TryGetValue(entry.Roseta, out var occupied)) writeOccupancy?.Invoke(entry.Row, occupied.Single());
        }
        return new RenderResult(entries.Count, entries.Count(entry => entry.InvalidRoseta));
    }

    private static List<TemplateEntry> TemplateEntries(XDocument document, string rosetaColumn, string[] infrastructureColumns, IReadOnlyList<string> strings)
    {
        return document.Descendants(Spreadsheet + "row").Skip(1).Select(row =>
        {
            var raw = CellText(row, rosetaColumn, strings);
            var invalid = string.Equals(raw.Trim(), "#N/A", StringComparison.OrdinalIgnoreCase);
            var roseta = invalid ? null : NormalizeRoseta(raw);
            var infrastructure = infrastructureColumns.Any(column => Meaningful(CellText(row, column, strings)));
            return new TemplateEntry(row, roseta, infrastructure, invalid);
        }).Where(entry => entry.Roseta is not null || entry.HasInfrastructure || entry.InvalidRoseta).ToList();
    }

    private static void WriteRosetasOccupancy(XElement row, Occupancy item)
    {
        SetText(row, "D", item.Location); SetText(row, "E", item.Device); SetText(row, "F", item.Person); SetText(row, "G", ""); SetText(row, "H", "");
        if (item.UpdatedAt is double date) SetNumber(row, "I", date);
    }
    private static void WriteParcheoOccupancy(XElement row, Occupancy item)
    {
        SetText(row, "I", item.Location); SetText(row, "J", item.Device); SetText(row, "K", item.Person); SetText(row, "L", ""); SetText(row, "M", "");
    }

    private static void CopyTemplate(string outputPath)
    {
        var resource = Assembly.GetExecutingAssembly().GetManifestResourceNames().SingleOrDefault(name => name.EndsWith(".ParcheoCampoTemplate.xlsx", StringComparison.Ordinal)) ?? throw new FileNotFoundException("No se encontró la plantilla de parcheo embebida.");
        using var input = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource) ?? throw new FileNotFoundException("No se pudo abrir la plantilla de parcheo embebida.");
        using var output = File.Create(outputPath); input.CopyTo(output);
    }
    private static XDocument LoadWorksheet(ZipArchive archive, string name) { var entry = archive.GetEntry(name) ?? throw new FileNotFoundException($"No existe {name} en la plantilla."); using var stream = entry.Open(); return XDocument.Load(stream, LoadOptions.PreserveWhitespace); }
    private static void SaveWorksheet(ZipArchive archive, string name, XDocument document) { var entry = archive.GetEntry(name) ?? throw new FileNotFoundException($"No existe {name} en la plantilla."); entry.Delete(); using var stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open(); document.Save(stream); }
    private static IReadOnlyList<string> LoadSharedStrings(ZipArchive archive) { var entry = archive.GetEntry("xl/sharedStrings.xml"); if (entry is null) return []; using var stream = entry.Open(); var document = XDocument.Load(stream); return document.Descendants(Spreadsheet + "si").Select(item => string.Concat(item.Descendants(Spreadsheet + "t").Select(text => text.Value))).ToArray(); }
    private static void Renumber(XElement row, int number) { row.SetAttributeValue("r", number); foreach (var cell in row.Elements(Spreadsheet + "c")) cell.SetAttributeValue("r", Column(cell.Attribute("r")?.Value) + number); }
    private static XElement Cell(XElement row, string column) { var cell = row.Elements(Spreadsheet + "c").FirstOrDefault(item => Column(item.Attribute("r")?.Value) == column); if (cell is not null) return cell; cell = new XElement(Spreadsheet + "c", new XAttribute("r", column + row.Attribute("r")?.Value)); row.Add(cell); return cell; }
    private static void Clear(XElement row, string column) { var cell = Cell(row, column); cell.SetAttributeValue("t", null); cell.RemoveNodes(); }
    private static void SetText(XElement row, string column, string value) { if (string.IsNullOrWhiteSpace(value)) { Clear(row, column); return; } var cell = Cell(row, column); cell.SetAttributeValue("t", "inlineStr"); cell.RemoveNodes(); cell.Add(new XElement(Spreadsheet + "is", new XElement(Spreadsheet + "t", new XAttribute(Xml + "space", "preserve"), value))); }
    private static void SetNumber(XElement row, string column, double value) { var cell = Cell(row, column); cell.SetAttributeValue("t", null); cell.RemoveNodes(); cell.Add(new XElement(Spreadsheet + "v", value.ToString(CultureInfo.InvariantCulture))); }
    private static string CellText(XElement row, string column, IReadOnlyList<string> strings) { var cell = row.Elements(Spreadsheet + "c").FirstOrDefault(item => Column(item.Attribute("r")?.Value) == column); if (cell is null) return ""; if (cell.Attribute("t")?.Value == "inlineStr") return string.Concat(cell.Descendants(Spreadsheet + "t").Select(text => text.Value)); var value = cell.Element(Spreadsheet + "v")?.Value ?? ""; return cell.Attribute("t")?.Value == "s" && int.TryParse(value, out var index) && index >= 0 && index < strings.Count ? strings[index] : value; }
    private static string Column(string? reference) => reference is null ? "" : new string(reference.TakeWhile(char.IsLetter).ToArray());
    private static Dictionary<string, JsonObject> Index(JsonArray? source, string key) => source?.OfType<JsonObject>().Where(item => Text(item[key]).Length > 0).GroupBy(item => Text(item[key])).ToDictionary(group => group.Key, group => group.Last()) ?? [];
    private static string Lookup(Dictionary<string, JsonObject> source, string key, params string[] fields) => source.TryGetValue(key, out var item) ? First(fields.Select(field => Text(item[field])).Append(key).ToArray()) : key;
    private static string First(params string[] values) => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? "";
    private static string Text(JsonNode? node) => node?.ToString() ?? "";
    private static bool Meaningful(string value) => !string.IsNullOrWhiteSpace(value) && value.Trim() is not "-" and not "#N/A";
    private static string? NormalizeRoseta(string value) => Meaningful(value) ? value.Trim() : null;
    private static double? DateForExcel(string value) => DateTimeOffset.TryParse(value, out var date) ? date.LocalDateTime.ToOADate() : null;

    private sealed record Occupancy(string WorkstationId, string Roseta, string Person, string Device, string Location, double? UpdatedAt);
    private sealed record TemplateEntry(XElement Row, string? Roseta, bool HasInfrastructure, bool InvalidRoseta);
    private sealed record RenderResult(int Rows, int SkippedInvalid);
}

internal sealed record XlsxExportResult(int RosetasRowsFilled, int RosetasFromTemplate = 0, int RosetasFromPlan = 0, int RosetasInBoth = 0, int RosetasOnlyFromPlan = 0, int RenderedRows = 0, int DuplicateRosetas = 0, int TemplateRowsWithoutRoseta = 0, int TemplateRowsSkippedInvalidRoseta = 0, IReadOnlyList<string>? DuplicateRosetaIds = null, IReadOnlyDictionary<string, string[]>? DuplicateRosetaWorkstations = null);
