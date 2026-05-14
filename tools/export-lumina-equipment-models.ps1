param(
    [Parameter(Mandatory = $true)]
    [string]$GameDataPath,

    [string]$DalamudLibPath = "$env:APPDATA\XIVLauncherCN\addon\Hooks\dev",

    [string]$OutputPath = ".\lumina-equipment-models.json"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $GameDataPath)) {
    throw "GameDataPath not found: $GameDataPath"
}

if (-not (Test-Path $DalamudLibPath)) {
    throw "DalamudLibPath not found: $DalamudLibPath"
}

$workDir = Join-Path ([IO.Path]::GetTempPath()) "glamourcollection-lumina-export"
$projectPath = Join-Path $workDir "LuminaModelExport.csproj"
$programPath = Join-Path $workDir "Program.cs"

New-Item -ItemType Directory -Path $workDir -Force | Out-Null

@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="Lumina">
      <HintPath>$([Security.SecurityElement]::Escape((Join-Path $DalamudLibPath "Lumina.dll")))</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Lumina.Excel">
      <HintPath>$([Security.SecurityElement]::Escape((Join-Path $DalamudLibPath "Lumina.Excel.dll")))</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
"@ | Set-Content -Encoding UTF8 -Path $projectPath

@'
using Lumina;
using Lumina.Data;
using Lumina.Excel.Sheets;
using System.Text.Encodings.Web;
using System.Text.Json;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: LuminaModelExport <gameDataPath> <outputPath>");
    return 2;
}

var gameDataPath = args[0];
var outputPath = Path.GetFullPath(args[1]);
var data = new GameData(gameDataPath);
var sheet = data.GetExcelSheet<Item>(Language.ChineseSimplified);
if (sheet is null)
{
    Console.Error.WriteLine("Could not load Item sheet.");
    return 3;
}

var rows = new List<Row>();
foreach (var item in sheet)
{
    if (item.RowId == 0 || item.EquipSlotCategory.RowId == 0)
        continue;

    var name = Text(item.Name);
    if (string.IsNullOrWhiteSpace(name))
        continue;

    rows.Add(new Row(
        item.RowId,
        name,
        Text(item.ItemUICategory.Value.Name),
        Text(item.ClassJobCategory.Value.Name),
        item.ItemUICategory.RowId,
        item.EquipSlotCategory.RowId,
        item.ModelMain,
        item.ModelSub,
        $"0x{item.ModelMain:X}",
        $"0x{item.ModelSub:X}",
        GetModelBaseId(item.ModelMain),
        GetModelBaseId(item.ModelSub),
        StrictKey(item),
        LooseKey(item)));
}

rows.Sort((left, right) => left.ItemId.CompareTo(right.ItemId));
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

var payload = new Payload(
    DateTimeOffset.Now,
    "Lumina Item 可装备物品模型字段导出。Strict 使用完整 ModelMain/ModelSub；Loose 使用拆出的主体模型 ID。",
    rows.Count,
    rows);

var options = new JsonSerializerOptions
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    WriteIndented = true,
};

File.WriteAllText(outputPath, JsonSerializer.Serialize(payload, options));
Console.WriteLine($"Exported {rows.Count} rows to {outputPath}");
return 0;

static string Text(object value) => value.ToString() ?? string.Empty;

static string StrictKey(Item item)
    => item.ModelMain == 0 && item.ModelSub == 0
        ? $"item:{item.RowId}"
        : $"model:{item.ItemUICategory.RowId}:{item.EquipSlotCategory.RowId}:{item.ModelMain}:{item.ModelSub}";

static string LooseKey(Item item)
    => item.ModelMain == 0 && item.ModelSub == 0
        ? $"item:{item.RowId}"
        : $"model:{item.ItemUICategory.RowId}:{item.EquipSlotCategory.RowId}:{GetModelBaseId(item.ModelMain)}:{GetModelBaseId(item.ModelSub)}";

static ulong GetModelBaseId(ulong model)
{
    if (model == 0)
        return 0;

    var low = model & 0xFFFF;
    if (low != 0)
        return low;

    for (var shift = 48; shift >= 16; shift -= 16)
    {
        var part = (model >> shift) & 0xFFFF;
        if (part != 0)
            return part;
    }

    return model;
}

public sealed record Payload(DateTimeOffset ExportedAt, string Note, int Count, IReadOnlyList<Row> Rows);

public sealed record Row(
    uint ItemId,
    string Name,
    string CategoryName,
    string ClassJobCategoryName,
    uint ItemUICategoryId,
    uint EquipSlotCategoryId,
    ulong ModelMain,
    ulong ModelSub,
    string ModelMainHex,
    string ModelSubHex,
    ulong ModelMainBase,
    ulong ModelSubBase,
    string StrictAppearanceKey,
    string LooseAppearanceKey);
'@ | Set-Content -Encoding UTF8 -Path $programPath

dotnet run --project $projectPath -- "$GameDataPath" "$OutputPath"
