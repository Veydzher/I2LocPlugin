using AssetsTools.NET;
using AssetsTools.NET.Extra;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Text;
using UABEAvalonia;
using UABEAvalonia.Plugins;

namespace I2LocPlugin
{
    public static class I2LocAssetHelper
    {
        public static string GetUContainerExtension(AssetContainer item)
        {
            string ucont = item.Container;
            if (Path.GetFileName(ucont) != Path.GetFileNameWithoutExtension(ucont))
            {
                return Path.GetExtension(ucont);
            }

            return string.Empty;
        }

        public static bool IsI2LanguageSource(AssetContainer cont)
        {
            if (cont.ClassId != (int)AssetClassID.MonoBehaviour)
                return false;

            string c = cont.Container?.ToLowerInvariant() ?? string.Empty;
            return c.Contains("i2languages") || c.Contains("languagesource");
        }
    }

    /// <summary>
    /// Enhanced I2 CSV helper with robust RFC 4180 compliant parsing.
    /// Supports adding new terms and languages during import.
    /// </summary>
    internal static class I2CsvUtil
    {
        /// <summary>
        /// RFC 4180 compliant CSV escaping.
        /// </summary>
        private static string CsvEscape(string value)
        {
            if (value == null)
                return "";

            bool needQuotes =
                value.Contains(",") ||
                value.Contains("\"") ||
                value.Contains("\r") ||
                value.Contains("\n");

            var v = value.Replace("\"", "\"\"");
            return needQuotes ? $"\"{v}\"" : v;
        }

        /// <summary>
        /// RFC 4180 compliant CSV line parser.
        /// Handles quoted fields, escaped quotes, and embedded newlines.
        /// </summary>
        private static List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            if (line == null)
                return result;

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '\"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '\"')
                    {
                        // escaped quote
                        sb.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            result.Add(sb.ToString());
            return result;
        }

        /// <summary>
        /// Splits CSV text into lines while preserving quoted multi-line fields.
        /// </summary>
        private static List<string> SplitCsvLines(string csvText)
        {
            var lines = new List<string>();
            var currentLine = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csvText.Length; i++)
            {
                char c = csvText[i];

                if (c == '\"')
                {
                    currentLine.Append(c);
                    // Check for escaped quote
                    if (i + 1 < csvText.Length && csvText[i + 1] == '\"')
                    {
                        currentLine.Append('\"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    // End of line
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine.ToString());
                        currentLine.Clear();
                    }
                    // Skip \r\n pairs
                    if (c == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                    {
                        i++;
                    }
                }
                else
                {
                    currentLine.Append(c);
                }
            }

            // Add final line if exists
            if (currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
            }

            return lines;
        }

        private static string TermTypeToString(int termType)
        {
            return termType switch
            {
                0 => "Text",
                1 => "Font",
                2 => "Texture",
                3 => "AudioClip",
                4 => "GameObject",
                5 => "Sprite",
                6 => "Material",
                7 => "Child",
                8 => "Mesh",
                9 => "Custom",
                _ => termType.ToString()
            };
        }

        private static int TermTypeFromString(string termType)
        {
            return termType?.ToLowerInvariant() switch
            {
                "text" => 0,
                "font" => 1,
                "texture" => 2,
                "audioclip" => 3,
                "gameobject" => 4,
                "sprite" => 5,
                "material" => 6,
                "child" => 7,
                "mesh" => 8,
                "custom" => 9,
                _ => int.TryParse(termType, out int val) ? val : 0
            };
        }

        /// <summary>
        /// Export LanguageSourceData MonoBehaviour to CSV string.
        /// </summary>
        public static string ExportToCsv(AssetTypeValueField baseField)
        {
            var sb = new StringBuilder();

            // ===== Languages =====
            var langsArray = baseField["mSource.mLanguages.Array"];
            int langCount = langsArray.Children.Count;

            // header
            sb.Append(CsvEscape("Key"));
            sb.Append(',');
            sb.Append(CsvEscape("Type"));

            for (int i = 0; i < langCount; i++)
            {
                sb.Append(',');
                var lf = langsArray.Children[i];
                string name = lf["Name"].AsString;
                string code = lf["Code"].AsString;

                string header = string.IsNullOrEmpty(code)
                    ? name
                    : $"{name} [{code}]";

                sb.Append(CsvEscape(header));
            }
            sb.AppendLine();

            // ===== Terms =====
            var termsArray = baseField["mSource.mTerms.Array"];
            int termCount = termsArray.Children.Count;

            for (int t = 0; t < termCount; t++)
            {
                var termField = termsArray.Children[t];

                string term = termField["Term"].AsString;
                int termTypeInt = termField["TermType"].AsInt;
                string termType = TermTypeToString(termTypeInt);

                sb.Append(CsvEscape(term));
                sb.Append(',');
                sb.Append(CsvEscape(termType));

                var langVals = termField["Languages.Array"];
                for (int li = 0; li < langCount; li++)
                {
                    sb.Append(',');
                    string val = li < langVals.Children.Count
                        ? langVals.Children[li].AsString
                        : string.Empty;

                    // encode real newlines as \n for single-line display
                    if (val != null)
                    {
                        val = val.Replace("\r\n", "\n")
                                 .Replace("\r", "\n")
                                 .Replace("\n", "\\n");
                    }

                    sb.Append(CsvEscape(val ?? string.Empty));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private struct CsvLangHeader
        {
            public string Name;
            public string Code;
        }

        private static CsvLangHeader ParseLangHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
                return new CsvLangHeader { Name = "", Code = "" };

            int idx = header.LastIndexOf('[');
            int idx2 = header.LastIndexOf(']');

            if (idx >= 0 && idx2 > idx)
            {
                string name = header.Substring(0, idx).Trim();
                string code = header.Substring(idx + 1, idx2 - idx - 1).Trim();
                return new CsvLangHeader { Name = name, Code = code };
            }

            return new CsvLangHeader { Name = header.Trim(), Code = "" };
        }

        /// <summary>
        /// Creates a new LanguageData field by manually copying structure from existing one.
        /// </summary>
        private static AssetTypeValueField CreateLanguageField(
    AssetTypeValueField existingLang, string name, string code)
        {
            var langField = ValueBuilder.DefaultValueFieldFromTemplate(
                existingLang.TemplateField);

            if (langField == null)
                return null;

            try
            {
                var nameField = langField.Get("Name");
                if (nameField != null)
                    nameField.AsString = name;

                var codeField = langField.Get("Code");
                if (codeField != null)
                    codeField.AsString = code;

                var flagsField = langField.Get("Flags");
                if (flagsField != null && flagsField.Value != null)
                    flagsField.AsUInt = 0;
            }
            catch
            {
                return null;
            }

            return langField;
        }

        /// <summary>
        /// Creates a new TermData field by manually copying structure from existing one.
        /// </summary>
        private static AssetTypeValueField CreateTermField(AssetTypeValueField existingTerm, string term, int termType, int langCount)
        {
            var termField = ValueBuilder.DefaultValueFieldFromTemplate(existingTerm.TemplateField);
            if (termField == null)
                return null;

            try
            {
                // Copy all non-array fields to preserve types
                for (int i = 0; i < existingTerm.Children.Count && i < termField.Children.Count; i++)
                {
                    var existingChild = existingTerm.Children[i];
                    var newChild = termField.Children[i];

                    // Skip Languages array - we'll handle it separately
                    if (existingChild.FieldName == "Languages")
                        continue;

                    // Copy the value
                    if (existingChild.Value != null && newChild.Value != null)
                    {
                        newChild.Value = existingChild.Value;
                    }
                }

                // Now override Term and TermType
                var termNameField = termField.Get("Term");
                if (termNameField != null) termNameField.AsString = term;

                var termTypeField = termField.Get("TermType");
                if (termTypeField != null) termTypeField.AsInt = termType;

                // Handle Languages array
                var langArrayField = termField.Get("Languages")?.Get("Array");
                var existingLangArray = existingTerm.Get("Languages")?.Get("Array");

                if (langArrayField != null && existingLangArray != null && existingLangArray.Children.Count > 0)
                {
                    langArrayField.Children.Clear();

                    // Add empty strings for each language
                    var stringTemplate = existingLangArray.Children[0].TemplateField;
                    for (int i = 0; i < langCount; i++)
                    {
                        var stringField = ValueBuilder.DefaultValueFieldFromTemplate(stringTemplate);
                        if (stringField != null)
                        {
                            stringField.AsString = string.Empty;
                            langArrayField.Children.Add(stringField);
                        }
                    }
                }
            }
            catch
            {
                return null;
            }

            return termField;
        }

        /// <summary>
        /// Import CSV into LanguageSourceData MonoBehaviour.
        /// Can add new languages and terms that don't exist yet.
        /// </summary>
        public static ImportResult ImportFromCsv(AssetTypeValueField baseField, string csvText)
        {
            var result = new ImportResult();

            if (string.IsNullOrWhiteSpace(csvText))
                return result;

            var lines = SplitCsvLines(csvText);
            if (lines.Count == 0)
                return result;

            // ---- header ----
            var headerCells = ParseCsvLine(lines[0]);
            if (headerCells.Count < 2)
                return result;

            int keyCol = 0;
            int typeCol = 1;
            int firstLangCol = 2;

            // Extract existing languages
            var langsArray = baseField["mSource.mLanguages.Array"];
            var existingLangs = new List<(string name, string code, int index)>();

            for (int i = 0; i < langsArray.Children.Count; i++)
            {
                var lf = langsArray.Children[i];
                string name = lf["Name"].AsString;
                string code = lf["Code"].AsString;
                existingLangs.Add((name, code, i));
            }

            // Map CSV columns to language indices (existing or new)
            var langColMapping = new Dictionary<int, int>();
            var newLanguages = new List<CsvLangHeader>();

            for (int col = firstLangCol; col < headerCells.Count; col++)
            {
                var parsed = ParseLangHeader(headerCells[col]);
                int foundIndex = -1;

                // Try to match existing language
                for (int i = 0; i < existingLangs.Count; i++)
                {
                    var (name, code, index) = existingLangs[i];

                    if (!string.IsNullOrEmpty(parsed.Code) &&
                        parsed.Code.Equals(code, System.StringComparison.OrdinalIgnoreCase))
                    {
                        foundIndex = index;
                        break;
                    }

                    if (!string.IsNullOrEmpty(parsed.Name) &&
                        parsed.Name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        foundIndex = index;
                        break;
                    }
                }

                if (foundIndex >= 0)
                {
                    langColMapping[col] = foundIndex;
                }
                else
                {
                    // New language to be added
                    int newIndex = existingLangs.Count + newLanguages.Count;
                    langColMapping[col] = newIndex;
                    newLanguages.Add(parsed);
                    result.LanguagesAdded++;
                }
            }

            // Add new languages to the asset
            if (newLanguages.Count > 0 && langsArray.Children.Count > 0)
            {
                // Use first existing language as template
                var templateLang = langsArray.Children[0];
                foreach (var lang in newLanguages)
                {
                    var newLangField = CreateLanguageField(templateLang, lang.Name, lang.Code);
                    if (newLangField != null)
                    {
                        langsArray.Children.Add(newLangField);
                    }
                }
            }

            int totalLangCount = existingLangs.Count + newLanguages.Count;

            // Build map of existing terms
            var termsArray = baseField["mSource.mTerms.Array"];
            var termMap = new Dictionary<string, AssetTypeValueField>();

            for (int t = 0; t < termsArray.Children.Count; t++)
            {
                var termField = termsArray.Children[t];
                string term = termField["Term"].AsString ?? "";
                if (!termMap.ContainsKey(term))
                    termMap.Add(term, termField);
            }

            // Read CSV rows
            var newTerms = new List<(string key, int type, Dictionary<int, string> values)>();

            for (int li = 1; li < lines.Count; li++)
            {
                var row = ParseCsvLine(lines[li]);
                if (row.Count == 0)
                    continue;

                if (keyCol >= row.Count)
                    continue;

                string key = row[keyCol];
                if (string.IsNullOrEmpty(key))
                    continue;

                string typeStr = typeCol < row.Count ? row[typeCol] : "Text";
                int termType = TermTypeFromString(typeStr);

                var values = new Dictionary<int, string>();
                foreach (var kvp in langColMapping)
                {
                    int csvCol = kvp.Key;
                    int langIndex = kvp.Value;

                    if (csvCol >= row.Count)
                        continue;

                    string val = row[csvCol] ?? string.Empty;
                    val = val.Replace("\\n", "\n");
                    values[langIndex] = val;
                }

                if (termMap.TryGetValue(key, out var termField))
                {
                    // Update existing term
                    var langVals = termField["Languages.Array"];

                    // Extend Languages array if needed
                    while (langVals.Children.Count < totalLangCount)
                    {
                        var elementTemplate = langVals.Children[0].TemplateField;
                        var newString = ValueBuilder.DefaultValueFieldFromTemplate(elementTemplate);
                        newString.AsString = string.Empty;
                        langVals.Children.Add(newString);
                    }

                    // Update values
                    foreach (var kvp in values)
                    {
                        int langIndex = kvp.Key;
                        string val = kvp.Value;

                        if (langIndex >= 0 && langIndex < langVals.Children.Count)
                        {
                            langVals.Children[langIndex].AsString = val;
                        }
                    }

                    result.TermsUpdated++;
                }
                else
                {
                    // New term to be added
                    newTerms.Add((key, termType, values));
                }
            }

            // Add new terms
            if (newTerms.Count > 0 && termsArray.Children.Count > 0)
            {
                // Use first existing term as template
                var templateTerm = termsArray.Children[0];
                foreach (var (key, termType, values) in newTerms)
                {
                    var newTermField = CreateTermField(templateTerm, key, termType, totalLangCount);
                    if (newTermField != null)
                    {
                        var langVals = newTermField.Get("Languages")?.Get("Array");
                        if (langVals != null)
                        {
                            foreach (var kvp in values)
                            {
                                int langIndex = kvp.Key;
                                string val = kvp.Value;

                                if (langIndex >= 0 && langIndex < langVals.Children.Count)
                                {
                                    langVals.Children[langIndex].AsString = val;
                                }
                            }
                        }

                        termsArray.Children.Add(newTermField);
                        result.TermsAdded++;
                    }
                }
            }

            return result;
        }

        public class ImportResult
        {
            public int TermsUpdated { get; set; }
            public int TermsAdded { get; set; }
            public int LanguagesAdded { get; set; }

            public override string ToString()
            {
                var parts = new List<string>();
                if (TermsUpdated > 0) parts.Add($"{TermsUpdated} term(s) updated");
                if (TermsAdded > 0) parts.Add($"{TermsAdded} term(s) added");
                if (LanguagesAdded > 0) parts.Add($"{LanguagesAdded} language(s) added");

                return parts.Count > 0 ? string.Join(", ", parts) : "No changes made";
            }
        }
    }

    public class ImportI2LocalizationOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Import I2 Localization CSV";

            if (action != UABEAPluginAction.Import)
                return false;

            if (selection == null || selection.Count == 0)
                return false;

            foreach (AssetContainer cont in selection)
            {
                if (!I2LocAssetHelper.IsI2LanguageSource(cont))
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            return await SingleImport(win, workspace, selection);
        }

        public async Task<bool> SingleImport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];
            AssetTypeValueField baseField = workspace.GetBaseField(cont);

            var filters = new List<FilePickerFileType>()
            {
                new FilePickerFileType("CSV files (*.csv)") { Patterns = new List<string>() { "*.csv" } },
                new FilePickerFileType("TSV files (*.tsv)") { Patterns = new List<string>() { "*.tsv" } },
                new FilePickerFileType("All files (*.*)") { Patterns = new List<string>() { "*" } }
            };

            var selectedFiles = await win.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = "Import I2 Localization Asset",
                FileTypeFilter = filters,
                AllowMultiple = false
            });

            string[] selectedFilePaths = FileDialogUtils.GetOpenFileDialogFiles(selectedFiles);
            if (selectedFilePaths.Length == 0)
                return false;

            string file = selectedFilePaths[0];
            string text = File.ReadAllText(file, Encoding.UTF8);

            // If TSV: convert tabs to commas before parsing
            if (Path.GetExtension(file).ToLowerInvariant() == ".tsv")
            {
                text = text.Replace('\t', ',');
            }

            var result = I2CsvUtil.ImportFromCsv(baseField, text);

            byte[] savedAsset = baseField.WriteToByteArray();
            var replacer = new AssetsReplacerFromMemory(
                cont.PathId, cont.ClassId, cont.MonoId, savedAsset);

            workspace.AddReplacer(cont.FileInstance, replacer, new MemoryStream(savedAsset));

            // Show import results
            string message = $"Import completed:\n{result}";
            await MessageBoxUtil.ShowDialog(win, "Import Results", message);

            return true;
        }
    }

    public class ExportI2LocalizationOption : UABEAPluginOption
    {
        public bool SelectionValidForPlugin(AssetsManager am, UABEAPluginAction action, List<AssetContainer> selection, out string name)
        {
            name = "Export I2 Localization CSV";

            if (action != UABEAPluginAction.Export)
                return false;

            if (selection == null || selection.Count == 0)
                return false;

            foreach (AssetContainer cont in selection)
            {
                if (!I2LocAssetHelper.IsI2LanguageSource(cont))
                    return false;
            }
            return true;
        }

        public async Task<bool> ExecutePlugin(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            return await SingleExport(win, workspace, selection);
        }

        public async Task<bool> SingleExport(Window win, AssetWorkspace workspace, List<AssetContainer> selection)
        {
            AssetContainer cont = selection[0];

            AssetTypeValueField baseField = workspace.GetBaseField(cont);
            string name = baseField["m_Name"].AsString;
            name = PathUtils.ReplaceInvalidPathChars(name);

            var filters = new List<FilePickerFileType>()
            {
                new FilePickerFileType("CSV files (*.csv)") { Patterns = new List<string>() { "*.csv" } },
                new FilePickerFileType("TSV files (*.tsv)") { Patterns = new List<string>() { "*.tsv" } }
            };

            string defaultExtension = "csv";

            string ucontExt = I2LocAssetHelper.GetUContainerExtension(cont);
            if (ucontExt != string.Empty)
            {
                string ucontExtNoDot = ucontExt[1..];
                string displayName = $"{ucontExtNoDot} files (*{ucontExt})";
                List<string> patterns = new List<string>() { "*" + ucontExt };
                filters.Insert(0, new FilePickerFileType(displayName) { Patterns = patterns });
                defaultExtension = ucontExtNoDot;
            }

            var selectedFile = await win.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions()
            {
                Title = "Export I2 Localization Asset",
                FileTypeChoices = filters,
                DefaultExtension = defaultExtension,
                SuggestedFileName = $"{name}-{Path.GetFileName(cont.FileInstance.path)}-{cont.PathId}"
            });

            string selectedFilePath = FileDialogUtils.GetSaveFileDialogFile(selectedFile);
            if (selectedFilePath == null)
                return false;

            string csv = I2CsvUtil.ExportToCsv(baseField);

            // If user picked .tsv, convert commas to tabs
            if (Path.GetExtension(selectedFilePath).ToLowerInvariant() == ".tsv")
            {
                csv = csv.Replace(',', '\t');
            }

            File.WriteAllText(selectedFilePath, csv, Encoding.UTF8);

            return true;
        }
    }

    public class I2LocAssetPlugin : UABEAPlugin
    {
        public PluginInfo Init()
        {
            PluginInfo info = new PluginInfo();
            info.name = "I2 Localization CSV Import/Export";

            info.options = new List<UABEAPluginOption>();
            info.options.Add(new ImportI2LocalizationOption());
            info.options.Add(new ExportI2LocalizationOption());
            return info;
        }
    }
}