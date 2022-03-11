using System.Text.Json;
using System.Text.Json.Nodes;

namespace MergeJson;

public class MergeJsonFiles
{
    private static readonly JsonDocumentOptions _jsonDocumentOptions = new() 
    { 
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip
    };

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        AllowTrailingCommas = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Allow,
    };

    public MergeJsonFiles(string sourceFilePath, string targetFilePath)
    {
        SourceFile = sourceFilePath;
        TargetFile = targetFilePath;
    }

    public string SourceFile { get; private set; }

    public string TargetFile { get; private set; }


    private MergeType _stringMergeType = MergeType.Merge;
    public string StringMergeType
    {
        get { return _stringMergeType.ToString(); }
        set { _stringMergeType = (MergeType)Enum.Parse(typeof(MergeType), value, true); }
    }

    private MergeType _arrayMergeType = MergeType.Merge;
    public string ArrayMergeType
    {
        get { return _arrayMergeType.ToString(); }
        set { _arrayMergeType = (MergeType)Enum.Parse(typeof(MergeType), value, true); }
    }

    public bool Execute()
    {
        string sourceFileText, targetFileText;
        JsonDocument? sourceDoc, targetDoc;

        // Load files.
        try
        {
            sourceFileText = LoadSourceFile();
            targetFileText = LoadTargetFile();
        }
        catch
        {
            return false;
        }

        try
        {
            sourceDoc = ParseSourceFile(sourceFileText);
            targetDoc = ParseTargetFile(targetFileText);
        }
        catch
        {
            
            return false;
        }

        // apply source to target
        JsonObject mergedObj;
        try
        {
            mergedObj = MergeObjects(sourceDoc.RootElement, targetDoc.RootElement, JsonObject.Create(targetDoc.RootElement));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error merging objects.", ex.Message);
            return false;
        }

        try
        {
            targetFileText = SaveTargetFile(mergedObj);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error writing merged json to {nameof(TargetFile)}.", ex.Message);
            return false;
        }

        Console.WriteLine($"Success Merging json files and updating {nameof(TargetFile)}.");
        return true;
    }

    private string SaveTargetFile(JsonObject mergedObj)
    {
        string targetFileText = mergedObj.ToJsonString(_jsonSerializerOptions);
        File.WriteAllText(TargetFile, targetFileText);
        return targetFileText;
    }

    private static JsonDocument ParseTargetFile(string targetFileText)
    {
        try
        {
            Console.WriteLine($"Parsing json for {nameof(TargetFile)}");
            JsonDocument targetDoc = JsonDocument.Parse(targetFileText, _jsonDocumentOptions);
            return targetDoc;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing Target file as {nameof(JsonDocument)}.", ex.Message);
            throw;
        }
    }

    private static JsonDocument ParseSourceFile(string sourceFileText)
    {
        try
        {
            Console.WriteLine($"Parsing json for {nameof(SourceFile)}");
            JsonDocument sourceDoc = JsonDocument.Parse(sourceFileText, _jsonDocumentOptions);
            return sourceDoc;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing Source file as {nameof(JsonDocument)}.", ex.Message);
            throw;
        }
    }

    private string LoadTargetFile()
    {
        try
        {
            Console.WriteLine($"Loading {nameof(TargetFile)} file from path: {TargetFile}");
            string targetFileText = File.ReadAllText(TargetFile);
            return targetFileText;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error loading Target file from provided path.", ex.Message);
            throw;
        }
    }

    private string LoadSourceFile()
    {
        try
        {
            Console.WriteLine($"Loading {nameof(SourceFile)} file from path: {SourceFile}");
            string sourceFileText = File.ReadAllText(SourceFile);
            return sourceFileText;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error loading Source file from provided path.", ex.Message);
            throw;
        }
    }


    /// <summary>
    /// Recursive function to merge Json objects
    /// </summary>
    /// <param name="sourceElement">template item to apply</param>
    /// <param name="targetElement">json element mirroring targetObj</param>
    /// <param name="targetObj">object to be applied to</param>
    /// <returns></returns>
    private JsonObject MergeObjects(JsonElement sourceElement, JsonElement targetElement, JsonObject? targetObj = null)
    {
        targetObj ??= JsonObject.Create(targetElement)!;

        foreach (var item in sourceElement.EnumerateObject())
        {
            JsonElement sourcePropValue = item.Value;
            JsonElement targetPropValue;

            if (targetElement.TryGetProperty(item.Name, out targetPropValue))
            {
                if (targetPropValue.ValueKind == JsonValueKind.Null)
                {
                    // apply full object
                    targetObj.Add(item.Name, JsonNode.Parse(sourcePropValue.GetRawText()));
                }

                // check for type mismatch
                if (sourcePropValue.ValueKind != targetPropValue.ValueKind)
                {
                    throw new Exception("Type mismatch on items");
                }

                targetObj[item.Name] = sourcePropValue.ValueKind switch
                {
                    JsonValueKind.Object => MergeObjects(sourcePropValue, targetPropValue),
                    JsonValueKind.Undefined => throw new NotImplementedException(),
                    JsonValueKind.Array => MergeArray(targetPropValue, sourcePropValue),
                    JsonValueKind.String => MergeString(targetPropValue.GetRawText(), sourcePropValue.GetRawText()),
                    _ => JsonNode.Parse(sourcePropValue.GetRawText())
                };
            }
            else
            {
                // doesn't exist, apply full object                    
                targetObj.Add(item.Name, JsonNode.Parse(sourcePropValue.GetRawText()));
            }
        }

        return targetObj;
    }

    private JsonArray MergeArray(JsonElement targetArray, JsonElement sourceArray)
    {
        if (_arrayMergeType == MergeType.Clobber)
        {
            return JsonArray.Create(sourceArray)!;
        }

        var finalArray = targetArray.EnumerateArray().Select(x => x.GetRawText().Trim()).ToList();

        // for each item in source, if not found append to temp array
        foreach (var item in sourceArray.EnumerateArray())
        {
            string strRaw = item.GetRawText().Trim().ToLower();
            if (finalArray.Any(x => x.ToLower() == strRaw))
            {
                continue;
            }

            finalArray.Add(strRaw);
        }

        // merge temp array to target
        string strFinal = $@"[{string.Join(@",", finalArray)}]";
        var result = JsonNode.Parse(strFinal)!.AsArray();

        return result;
    }

    private JsonNode MergeString(string targetString, string sourceString, char delimiter = ',')
    {
        if (_stringMergeType == MergeType.Clobber)
        {
            return JsonNode.Parse(sourceString)!;
        }

        targetString = targetString.Trim('"');
        sourceString = sourceString.Trim('"');
        var targetArray = targetString.Split(delimiter).ToList();

        // apply sourceString on targetString
        foreach (var item in sourceString.Split(delimiter))
        {
            string str = item.Trim();
            if (targetArray.Contains(str))
            {
                continue;
            }
            targetArray.Add(str);
        }

        string strResult = $@"""{string.Join(delimiter.ToString(), targetArray)}""";
        JsonNode result = JsonNode.Parse(strResult)!;

        return result;
    }
}

