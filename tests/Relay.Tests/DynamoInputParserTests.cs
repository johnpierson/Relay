using System.Text.Json;
using Relay.Classes;
using Relay.Utilities;

namespace Relay.Tests;

public sealed class DynamoInputParserTests : IDisposable
{
    private readonly List<string> _tempFiles = new();
    private readonly List<string> _tempDirectories = new();

    public void Dispose()
    {
        foreach (var file in _tempFiles)
        {
            try { File.Delete(file); } catch { }
        }

        foreach (var directory in _tempDirectories)
        {
            try { Directory.Delete(directory, true); } catch { }
        }
    }

    /// <summary>
    /// Writes the graph into a directory of its own so that "beside the source graph"
    /// and "in the system temp folder" are genuinely different locations — writing the
    /// source into the temp root would make both write candidates resolve to the same
    /// directory and any placement assertion vacuous.
    /// </summary>
    private string WriteGraph(string json)
    {
        var directory = CreateGraphDirectory();
        var path = Path.Combine(directory, $"relay_test_{Guid.NewGuid():N}.dyn");
        File.WriteAllText(path, json, System.Text.Encoding.UTF8);
        return path;
    }

    private string CreateGraphDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"relay_src_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        _tempDirectories.Add(directory);
        return directory;
    }

    private void TrackTemp(string path)
    {
        if (!string.IsNullOrEmpty(path))
            _tempFiles.Add(path);
    }

    /// <summary>
    /// A representative graph with one node of every supported input kind plus one
    /// unsupported custom node, an annotation group, and a non-input node.
    /// </summary>
    private const string SampleGraph = """
    {
      "Uuid": "3c9d0464-8643-5ffe-96e5-ab1769818209",
      "Name": "Sample Inputs",
      "Inputs": [
        {
          "Id": "id-string",
          "Name": "Prefix",
          "Type": "string",
          "Value": "hello",
          "Description": "Prefix applied to view names"
        },
        {
          "Id": "id-double-slider",
          "Name": "Scale Factor",
          "Type": "number",
          "Value": "5.5",
          "MaximumValue": 10.0,
          "MinimumValue": 0.0,
          "StepValue": 0.1,
          "NumberType": "Double"
        },
        {
          "Id": "id-int-slider",
          "Name": "Count",
          "Type": "integer",
          "Value": "3",
          "MaximumValue": 10,
          "MinimumValue": 0,
          "StepValue": 1,
          "NumberType": "Integer"
        },
        {
          "Id": "id-bool",
          "Name": "Include Existing",
          "Type": "boolean",
          "Value": "true"
        },
        {
          "Id": "id-categories",
          "Name": "Category",
          "Type": "selection",
          "Type2": "dropdownSelection",
          "Value": "OST_Walls"
        },
        {
          "Id": "id-selection",
          "Name": "Wall To Modify",
          "Type": "selection",
          "Type2": "hostSelection",
          "Value": "unique-id-original"
        },
        {
          "Id": "id-filepath",
          "Name": "Excel File",
          "Type": "string",
          "Value": "..\\data.xlsx"
        },
        {
          "Id": "id-unsupported",
          "Name": "Fancy Input",
          "Type": "",
          "Value": ""
        }
      ],
      "Nodes": [
        {
          "ConcreteType": "CoreNodeModels.Input.StringInput, CoreNodeModels",
          "Id": "id-string",
          "NodeType": "StringInputNode",
          "InputValue": "hello"
        },
        {
          "ConcreteType": "CoreNodeModels.Input.DoubleSlider, CoreNodeModels",
          "Id": "id-double-slider",
          "NodeType": "NumberInputNode",
          "InputValue": 5.5,
          "MaximumValue": 10.0,
          "MinimumValue": 0.0,
          "StepValue": 0.1
        },
        {
          "ConcreteType": "CoreNodeModels.Input.IntegerSlider64Bit, CoreNodeModels",
          "Id": "id-int-slider",
          "NodeType": "NumberInputNode",
          "InputValue": 3,
          "MaximumValue": 10,
          "MinimumValue": 0,
          "StepValue": 1
        },
        {
          "ConcreteType": "CoreNodeModels.Input.BooleanSelector, CoreNodeModels",
          "Id": "id-bool",
          "NodeType": "BooleanInputNode",
          "InputValue": true
        },
        {
          "ConcreteType": "DSRevitNodesUI.Categories, DSRevitNodesUI",
          "Id": "id-categories",
          "NodeType": "ExtensionNode",
          "SelectedIndex": 5,
          "SelectedString": "OST_Walls"
        },
        {
          "ConcreteType": "Dynamo.Nodes.DSModelElementSelection, DSRevitNodesUI",
          "Id": "id-selection",
          "NodeType": "ExtensionNode",
          "InstanceId": [
            "unique-id-original"
          ]
        },
        {
          "ConcreteType": "CoreNodeModels.Input.Filename, CoreNodeModels",
          "Id": "id-filepath",
          "NodeType": "ExtensionNode",
          "HintPath": "C:\\proj\\data.xlsx",
          "InputValue": "..\\data.xlsx"
        },
        {
          "ConcreteType": "SomeVendor.FancyInputNode, VendorPackage",
          "Id": "id-unsupported",
          "NodeType": "ExtensionNode"
        },
        {
          "ConcreteType": "CoreNodeModels.Input.StringInput, CoreNodeModels",
          "Id": "id-not-an-input",
          "NodeType": "StringInputNode",
          "InputValue": "not marked"
        }
      ],
      "View": {
        "NodeViews": [
          { "Id": "id-string", "Name": "Prefix", "IsSetAsInput": true },
          { "Id": "id-double-slider", "Name": "Scale Factor", "IsSetAsInput": true },
          { "Id": "id-int-slider", "Name": "Count", "IsSetAsInput": true },
          { "Id": "id-bool", "Name": "Include Existing", "IsSetAsInput": true },
          { "Id": "id-categories", "Name": "Category", "IsSetAsInput": true },
          { "Id": "id-selection", "Name": "Wall To Modify", "IsSetAsInput": true },
          { "Id": "id-filepath", "Name": "Excel File", "IsSetAsInput": true },
          { "Id": "id-unsupported", "Name": "Fancy Input", "IsSetAsInput": true },
          { "Id": "id-not-an-input", "Name": "Other", "IsSetAsInput": false }
        ],
        "Annotations": [
          {
            "Title": "Naming",
            "Nodes": [ "id-string", "id-bool" ]
          }
        ]
      }
    }
    """;

    // -------------------------------------------------------------------
    // Discovery
    // -------------------------------------------------------------------

    [Fact]
    public void ParsesAllDeclaredInputsInNodeViewOrder()
    {
        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(SampleGraph));

        Assert.Equal("Sample Inputs", result.GraphName);
        Assert.True(result.HasInputs);
        Assert.Equal(
            new[]
            {
                "id-string", "id-double-slider", "id-int-slider", "id-bool",
                "id-categories", "id-selection", "id-filepath", "id-unsupported"
            },
            result.Inputs.Select(i => i.Id).ToArray());
        Assert.DoesNotContain(result.Inputs, i => i.Id == "id-not-an-input");
    }

    [Fact]
    public void MapsSupportedInputKinds()
    {
        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(SampleGraph));
        var byId = result.Inputs.ToDictionary(i => i.Id);

        Assert.Equal(DynamoInputType.String, byId["id-string"].DataType);
        Assert.Equal(DynamoInputType.Number, byId["id-double-slider"].DataType);
        Assert.Equal(DynamoInputType.Integer, byId["id-int-slider"].DataType);
        Assert.Equal(DynamoInputType.Boolean, byId["id-bool"].DataType);
        Assert.Equal(DynamoInputType.Dropdown, byId["id-categories"].DataType);
        Assert.Equal(DynamoInputType.Selection, byId["id-selection"].DataType);
        Assert.Equal(DynamoInputType.FilePath, byId["id-filepath"].DataType);
        Assert.Equal(DynamoInputType.Unknown, byId["id-unsupported"].DataType);
    }

    [Fact]
    public void ReadsNamesDefaultsAndSliderBounds()
    {
        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(SampleGraph));
        var byId = result.Inputs.ToDictionary(i => i.Id);

        Assert.Equal("Prefix", byId["id-string"].Name);
        Assert.Equal("hello", byId["id-string"].DefaultValue);
        Assert.Equal("Prefix applied to view names", byId["id-string"].Description);

        var slider = byId["id-double-slider"];
        Assert.Equal(5.5, Assert.IsType<double>(slider.DefaultValue));
        Assert.Equal(0.0, slider.MinValue);
        Assert.Equal(10.0, slider.MaxValue);
        Assert.Equal(0.1, slider.StepValue);

        var intSlider = byId["id-int-slider"];
        Assert.Equal(3, Assert.IsType<int>(intSlider.DefaultValue));
        Assert.Equal(0.0, intSlider.MinValue);
        Assert.Equal(10.0, intSlider.MaxValue);
        Assert.Equal(1.0, intSlider.StepValue);

        Assert.Equal(true, byId["id-bool"].DefaultValue);
    }

    [Fact]
    public void ReadsDropdownAndSelectionMetadata()
    {
        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(SampleGraph));
        var byId = result.Inputs.ToDictionary(i => i.Id);

        var dropdown = byId["id-categories"];
        Assert.Equal(5, dropdown.SelectedIndex);
        Assert.Equal("OST_Walls", dropdown.SelectedString);

        var selection = byId["id-selection"];
        Assert.Equal("unique-id-original", selection.SelectionIdentifier);
        Assert.False(selection.IsMultipleSelection);
    }

    [Fact]
    public void FilePathPrefersAbsoluteHintPath()
    {
        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(SampleGraph));
        var filePath = result.Inputs.Single(i => i.Id == "id-filepath");

        Assert.Equal(DynamoInputType.FilePath, filePath.DataType);
        Assert.Equal("C:\\proj\\data.xlsx", filePath.DefaultValue);
    }

    [Fact]
    public void AssignsGroupNamesFromAnnotations()
    {
        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(SampleGraph));
        var byId = result.Inputs.ToDictionary(i => i.Id);

        Assert.Equal("Naming", byId["id-string"].GroupName);
        Assert.Equal("Naming", byId["id-bool"].GroupName);
        Assert.Null(byId["id-double-slider"].GroupName);
    }

    [Fact]
    public void GraphWithoutDeclaredInputsHasNoInputs()
    {
        var json = """
        {
          "Name": "No Inputs",
          "Inputs": [],
          "Nodes": [
            {
              "ConcreteType": "CoreNodeModels.Input.StringInput, CoreNodeModels",
              "Id": "id-a",
              "InputValue": "x"
            }
          ],
          "View": {
            "NodeViews": [ { "Id": "id-a", "Name": "A", "IsSetAsInput": false } ]
          }
        }
        """;

        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(json));

        Assert.False(result.HasInputs);
    }

    [Fact]
    public void MalformedJsonReturnsEmptyInputsWithoutThrowing()
    {
        var result = DynamoInputParser.ParseGraphInputs(WriteGraph("{ not valid json"));

        Assert.False(result.HasInputs);
    }

    [Fact]
    public void MissingFileReturnsEmptyInputsWithoutThrowing()
    {
        var result = DynamoInputParser.ParseGraphInputs(
            Path.Combine(Path.GetTempPath(), $"relay_missing_{Guid.NewGuid():N}.dyn"));

        Assert.False(result.HasInputs);
    }

    [Fact]
    public void UnrecognizedDropdownDetectedBySelectedIndexHeuristic()
    {
        var json = """
        {
          "Name": "Heuristics",
          "Inputs": [],
          "Nodes": [
            {
              "ConcreteType": "ThirdParty.SomeChooser, ThirdParty",
              "Id": "id-dd",
              "SelectedIndex": 2,
              "SelectedString": "Choice B"
            }
          ],
          "View": {
            "NodeViews": [ { "Id": "id-dd", "Name": "Chooser", "IsSetAsInput": true } ]
          }
        }
        """;

        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(json));
        var input = Assert.Single(result.Inputs);

        Assert.Equal(DynamoInputType.Dropdown, input.DataType);
        Assert.Equal(2, input.SelectedIndex);
        Assert.Equal("Choice B", input.SelectedString);
    }

    [Fact]
    public void DuplicateNodeViewEntriesProduceASingleInput()
    {
        // A hand-resolved .dyn merge conflict can duplicate a NodeViews entry. Two rows
        // for one node would render, and whatever the user typed into the first would be
        // discarded silently.
        var json = """
        {
          "Name": "Duplicated",
          "Inputs": [
            { "Id": "id-a", "Name": "Sheet Name", "Type": "string", "Value": "A101" }
          ],
          "Nodes": [
            {
              "ConcreteType": "CoreNodeModels.Input.StringInput, CoreNodeModels",
              "Id": "id-a",
              "InputValue": "A101"
            }
          ],
          "View": {
            "NodeViews": [
              { "Id": "id-a", "Name": "Sheet Name", "IsSetAsInput": true },
              { "Id": "id-a", "Name": "Sheet Name", "IsSetAsInput": true }
            ]
          }
        }
        """;

        var result = DynamoInputParser.ParseGraphInputs(WriteGraph(json));

        var input = Assert.Single(result.Inputs);
        Assert.Equal("id-a", input.Id);
    }

    // -------------------------------------------------------------------
    // Write-back
    // -------------------------------------------------------------------

    [Fact]
    public void ApplyUserValuesWritesEverySupportedKindToACopy()
    {
        var sourcePath = WriteGraph(SampleGraph);
        var sourceBytes = File.ReadAllBytes(sourcePath);

        var userValues = new Dictionary<string, object>
        {
            ["id-string"] = "world",
            ["id-double-slider"] = 7.5,
            ["id-int-slider"] = 4,
            ["id-bool"] = false,
            ["id-categories"] = new DropdownValue { SelectedIndex = 12, SelectedString = "OST_Doors" },
            ["id-selection"] = "unique-a,unique-b",
            ["id-filepath"] = "C:\\new\\input.xlsx"
        };

        var applied = DynamoInputParser.ApplyUserValues(sourcePath, userValues);
        TrackTemp(applied.Path);

        Assert.True(applied.Succeeded);
        Assert.Null(applied.Warning);
        var modifiedPath = applied.Path;

        Assert.NotEqual(sourcePath, modifiedPath);
        Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath)); // source untouched

        // The copy lives beside the source graph (Dynamo file trust is folder-based)
        // with a .relay_ prefix so ribbon sync never lists it as a graph.
        Assert.Equal(Path.GetDirectoryName(sourcePath), Path.GetDirectoryName(modifiedPath));
        Assert.NotEqual(Path.TrimEndingDirectorySeparator(Path.GetTempPath()),
                        Path.GetDirectoryName(modifiedPath));
        Assert.StartsWith(DynamoInputParser.TempGraphPrefix, Path.GetFileName(modifiedPath));

        using var doc = JsonDocument.Parse(File.ReadAllText(modifiedPath));
        var nodes = doc.RootElement.GetProperty("Nodes")
            .EnumerateArray()
            .ToDictionary(n => n.GetProperty("Id").GetString()!);

        Assert.Equal("world", nodes["id-string"].GetProperty("InputValue").GetString());
        Assert.Equal(7.5, nodes["id-double-slider"].GetProperty("InputValue").GetDouble());
        Assert.Equal(4, nodes["id-int-slider"].GetProperty("InputValue").GetInt32());
        Assert.False(nodes["id-bool"].GetProperty("InputValue").GetBoolean());

        Assert.Equal(12, nodes["id-categories"].GetProperty("SelectedIndex").GetInt32());
        Assert.Equal("OST_Doors", nodes["id-categories"].GetProperty("SelectedString").GetString());

        Assert.Equal(
            new[] { "unique-a", "unique-b" },
            nodes["id-selection"].GetProperty("InstanceId").EnumerateArray().Select(e => e.GetString()).ToArray());

        Assert.Equal("C:\\new\\input.xlsx", nodes["id-filepath"].GetProperty("InputValue").GetString());
        Assert.Equal("C:\\new\\input.xlsx", nodes["id-filepath"].GetProperty("HintPath").GetString());

        // Untouched node keeps its value
        Assert.Equal("not marked", nodes["id-not-an-input"].GetProperty("InputValue").GetString());
    }

    [Fact]
    public void ApplyUserValuesSyncsTopLevelInputsArray()
    {
        var sourcePath = WriteGraph(SampleGraph);

        var applied = DynamoInputParser.ApplyUserValues(sourcePath, new Dictionary<string, object>
        {
            ["id-string"] = "world",
            ["id-double-slider"] = 7.5,
            ["id-bool"] = false,
            ["id-categories"] = new DropdownValue { SelectedIndex = 12, SelectedString = "OST_Doors" }
        });
        TrackTemp(applied.Path);
        Assert.True(applied.Succeeded);

        using var doc = JsonDocument.Parse(File.ReadAllText(applied.Path));
        var inputs = doc.RootElement.GetProperty("Inputs")
            .EnumerateArray()
            .ToDictionary(n => n.GetProperty("Id").GetString()!);

        Assert.Equal("world", inputs["id-string"].GetProperty("Value").GetString());
        Assert.Equal("7.5", inputs["id-double-slider"].GetProperty("Value").GetString());
        Assert.Equal("false", inputs["id-bool"].GetProperty("Value").GetString());
        Assert.Equal("OST_Doors", inputs["id-categories"].GetProperty("Value").GetString());
        Assert.Equal(12, inputs["id-categories"].GetProperty("SelectedIndex").GetInt32());
    }

    [Fact]
    public void ApplyUserValuesOnMissingFileFailsInsteadOfRunningTheSourceGraph()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"relay_missing_{Guid.NewGuid():N}.dyn");

        var applied = DynamoInputParser.ApplyUserValues(missing, new Dictionary<string, object>
        {
            ["id"] = "value"
        });

        // Returning the source path here would run the graph with its saved values and
        // report success — the user's entries would vanish silently.
        Assert.False(applied.Succeeded);
        Assert.Null(applied.Path);
        Assert.Contains(missing, applied.Failure);
    }

    [Fact]
    public void ApplyUserValuesOnMalformedGraphFailsInsteadOfRunningTheSourceGraph()
    {
        var sourcePath = WriteGraph("{ not valid json");

        var applied = DynamoInputParser.ApplyUserValues(sourcePath, new Dictionary<string, object>
        {
            ["id-string"] = "world"
        });

        Assert.False(applied.Succeeded);
        Assert.Null(applied.Path);
        Assert.False(string.IsNullOrWhiteSpace(applied.Failure));
    }

    [Fact]
    public void ApplyUserValuesFallsBackToTempFolderWithATrustWarning()
    {
        var sourcePath = WriteGraph(SampleGraph);
        var sourceDirectory = Path.GetDirectoryName(sourcePath)!;

        // A read-only shared graph library: writing beside the graph is refused, the
        // system temp folder still works.
        var writer = new StubGraphFileWriter(path =>
            Path.GetDirectoryName(path) == sourceDirectory
                ? new UnauthorizedAccessException("Access to the path is denied.")
                : null);

        var applied = DynamoInputParser.ApplyUserValues(
            sourcePath,
            new Dictionary<string, object> { ["id-string"] = "world" },
            writer);

        Assert.True(applied.Succeeded);
        Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()),
                     Path.GetDirectoryName(applied.Path));

        // The user must be told the copy left the trusted folder, because Dynamo may
        // then refuse to evaluate it and the reason would otherwise be invisible.
        Assert.Contains("trust", applied.Warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("world", writer.Written[applied.Path]);
    }

    [Fact]
    public void ApplyUserValuesFailsWhenNoLocationIsWritable()
    {
        var sourcePath = WriteGraph(SampleGraph);
        var writer = new StubGraphFileWriter(_ => new IOException("There is not enough space on the disk."));

        var applied = DynamoInputParser.ApplyUserValues(
            sourcePath,
            new Dictionary<string, object> { ["id-string"] = "world" },
            writer);

        Assert.False(applied.Succeeded);
        Assert.Null(applied.Path);
        Assert.Contains("not enough space", applied.Failure);
    }

    [Fact]
    public void ApplyUserValuesRemovesAPartiallyWrittenCopy()
    {
        var sourcePath = WriteGraph(SampleGraph);
        var sourceDirectory = Path.GetDirectoryName(sourcePath)!;
        var writer = new StubGraphFileWriter(path =>
            Path.GetDirectoryName(path) == sourceDirectory
                ? new IOException("The write was interrupted.")
                : null);

        var applied = DynamoInputParser.ApplyUserValues(
            sourcePath,
            new Dictionary<string, object> { ["id-string"] = "world" },
            writer);

        Assert.True(applied.Succeeded);
        // The truncated file from the interrupted write must not be left in the graph
        // folder, where it would linger as junk beside the user's graphs.
        Assert.Single(writer.Deleted);
        Assert.Equal(sourceDirectory, Path.GetDirectoryName(writer.Deleted[0]));
    }

    private sealed class StubGraphFileWriter : IGraphFileWriter
    {
        private readonly Func<string, Exception> _failFor;

        internal StubGraphFileWriter(Func<string, Exception> failFor) => _failFor = failFor;

        internal Dictionary<string, string> Written { get; } = new(StringComparer.Ordinal);
        internal List<string> Deleted { get; } = new();

        public void WriteAllText(string path, string contents)
        {
            var failure = _failFor(path);
            if (failure != null) throw failure;
            Written[path] = contents;
        }

        public void Delete(string path)
        {
            Deleted.Add(path);
            Written.Remove(path);
        }
    }
}
