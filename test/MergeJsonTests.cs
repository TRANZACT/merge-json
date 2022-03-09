using Xunit;
using Bogus;
using Shouldly;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System;
using System.Threading.Tasks;

namespace MergeJson.Tests;

public class MergeJsonTests
{
    private const string EmptyJsonString = "{}";
    private static readonly Faker _faker = new();

    [Theory]
    [MemberData(nameof(MergeJsonData))]
    public async Task MergeTest(IJsonData left, IJsonData right)
    {
        string leftPath = await CreateJsonFileAsync(left);
        string rightPath = await CreateJsonFileAsync(right);

        MergeJsonFiles merge = new(leftPath, rightPath);
        var result = merge.Execute();

        result.ShouldBeTrue();

        if (left is EmptyJson && right is EmptyJson)
        {
            string emptyTarget = File.ReadAllText(rightPath);
            emptyTarget.ShouldBe(EmptyJsonString);
        }

        if (left is JsonA leftA) // Assert JsonA data is in target JSON
            await AssertJsonAToTarget(leftA, rightPath);

        if (right is JsonA rightA) // Assert JsonA data is in target JSON
            await AssertJsonAToTarget(rightA, rightPath);

        if (left is JsonB leftB) // Assert JsonB data is in target JSON
            await AssertJsonBToTarget(leftB, rightPath);

        if (right is JsonB rightB) // Assert JsonB data is in target JSON
            await AssertJsonBToTarget(rightB, rightPath);

        CleanUpTestFiles(leftPath, rightPath);
    }

    private async Task AssertJsonAToTarget(JsonA jsonA, string targetPath)
    {
        JsonA? targetA = await GetTargetJson<JsonA>(targetPath);
        targetA.ShouldNotBeNull();
        foreach (var tag in jsonA.Tags)
        {
            targetA.Tags.ShouldContain(tag);
        }
    }

    private async Task AssertJsonBToTarget(JsonB jsonB, string targetPath)
    {
        JsonB? targetB = await GetTargetJson<JsonB>(targetPath);
        targetB.ShouldNotBeNull();
        foreach (var id in jsonB.ChildrenIds)
        {
            targetB.ChildrenIds.ShouldContain(id);
        }
    }

    private async Task<T?> GetTargetJson<T>(string path)
        where T: IJsonData
    {
        using var stream = File.OpenRead(path);
        T? obj = await JsonSerializer.DeserializeAsync<T>(stream, IJsonData.JsonSerializerOptions);
        return obj;
    }

    private async Task<string> CreateJsonFileAsync(IJsonData jsonData)
    {
        string filename = Guid.NewGuid().ToString("N") + ".json";
        using FileStream fileStream = File.Create(filename);
        await jsonData.SerializeAsync(fileStream);
        await fileStream.DisposeAsync();

        return filename;
    }

    private static void CleanUpTestFiles(params string[] paths)
    {
        foreach (var path in paths)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    public static IEnumerable<IJsonData[]> MergeJsonData => 
        new List<IJsonData[]>
        {
            new IJsonData[]{ EmptyJson.Empty, EmptyJson.Empty },

            new IJsonData[]{ JsonA.Generate(), EmptyJson.Empty },
            new IJsonData[]{ EmptyJson.Empty, JsonA.Generate() },

            new IJsonData[]{ JsonB.Generate(), EmptyJson.Empty },
            new IJsonData[]{ EmptyJson.Empty, JsonB.Generate() },

            new IJsonData[]{ JsonA.Generate(), JsonB.Generate() },
            new IJsonData[]{ JsonB.Generate(), JsonA.Generate() },

            new IJsonData[]{ JsonA.Generate(), JsonA.Generate() },
            new IJsonData[]{ JsonB.Generate(), JsonB.Generate() },
        };

    public interface IJsonData 
    {
        public static JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);
        Task SerializeAsync(Stream fileStream);
    }

    private record EmptyJson() : IJsonData
    {
        public static EmptyJson Empty => new();

        public string Serialize() => JsonSerializer.Serialize(this, IJsonData.JsonSerializerOptions);
        public async Task SerializeAsync(Stream fileStream) => await JsonSerializer.SerializeAsync(fileStream, this, IJsonData.JsonSerializerOptions);
    }

    private record JsonA(string Name, string[] Tags) : IJsonData
    {
        public static JsonA Generate() => 
            new(
                _faker.Person.FullName,
                _faker.Random.WordsArray(1, 10)
                             .Select(w => w.ToLower())
                             .ToArray()
            );

        public string Serialize() => JsonSerializer.Serialize(this, IJsonData.JsonSerializerOptions);
        public async Task SerializeAsync(Stream fileStream) => await JsonSerializer.SerializeAsync(fileStream, this, IJsonData.JsonSerializerOptions);
    }

    private record JsonB(int Id, int[] ChildrenIds) : IJsonData
    {
        public static JsonB Generate() => 
            new(
                _faker.Random.Int(10, 9999),
                Enumerable.Range(0, _faker.Random.Int(0, 10)).Select(_ => _faker.Random.Int(0, 9999)).ToArray()
            );

        public string Serialize() => JsonSerializer.Serialize(this, IJsonData.JsonSerializerOptions);
        public async Task SerializeAsync(Stream fileStream) => await JsonSerializer.SerializeAsync(fileStream, this, IJsonData.JsonSerializerOptions);
    }
}