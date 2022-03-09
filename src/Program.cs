
string? sourceFilePath = args?.ElementAtOrDefault(0);

if (sourceFilePath is null)
{
    Console.Error.WriteLine("Source file path was not provided");
    return -2;
}

string? targetFilePath = args?.ElementAtOrDefault(1);
if (targetFilePath is null)
{
    Console.Error.WriteLine("Target json file path was not provided");
    return -3;
}

var mergeTask = new MergeJson.MergeJsonFiles(sourceFilePath, targetFilePath);
bool isSuccess = mergeTask.Execute();

if (!isSuccess)
{
    Console.Error.WriteLine("Failed to merge json files.");
    return -1;
}

Console.WriteLine("Successfully merged json files.");
return 0;

