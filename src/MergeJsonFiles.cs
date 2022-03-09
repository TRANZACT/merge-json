using System.Text.Json;
using System.Text.Json.Nodes;

namespace MergeJson;

public class MergeJsonFiles
{
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
        string? sourceFileText, targetFileText;
        JsonDocument? sourceDoc, targetDoc;

        // Load files.
        try
        {
            Console.WriteLine($"Loading {nameof(SourceFile)} file from path: {SourceFile}");
            sourceFileText = System.IO.File.ReadAllText(SourceFile);
            Console.WriteLine($"Loading {nameof(TargetFile)} file from path: {TargetFile}");
            targetFileText = System.IO.File.ReadAllText(TargetFile);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Error loading Source or Target file from provided path.", ex.Message);
            return false;
        }

        try
        {
            var jsonOptions = new JsonDocumentOptions() { AllowTrailingCommas = true };

            Console.WriteLine($"Parsing json for {nameof(SourceFile)}");
            sourceDoc = JsonDocument.Parse(sourceFileText, jsonOptions);
            Console.WriteLine($"Parsing json for {nameof(TargetFile)}");
            targetDoc = JsonDocument.Parse(targetFileText, jsonOptions);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error parsing Source or Target file as {nameof(JsonDocument)}.", ex.Message);
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

        // save target
        try
        {
            targetFileText = mergedObj.ToJsonString(new JsonSerializerOptions()
            {
                AllowTrailingCommas = true,
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });
            System.IO.File.WriteAllText(TargetFile, targetFileText);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error writing merged json to {nameof(TargetFile)}.", ex.Message);
            return false;
        }

        Console.WriteLine($"Success Merging json files and updating {nameof(TargetFile)}.");
        return true;
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

