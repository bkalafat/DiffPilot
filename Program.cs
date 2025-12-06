using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public class Program
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static async Task Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8;

        while (true)
        {
            var line = await Console.In.ReadLineAsync();
            if (line == null)
                break;

            line = line.Trim();

            if (string.IsNullOrEmpty(line))
                continue;

            JsonRcpRequest? request = null;

            try
            {
                request = JsonSerializer.Deserialize<JsonRcpRequest>(line, _jsonOptions);
            }
            catch (JsonException)
            {
                WriteError(null, -32700, "Parse error");
                continue;
            }

            if (request == null || request.Method == null)
            {
                WriteError(null, -32600, "Request was null after deserialization");
                continue;
            }

            // dispatch methods
            try
            {
                await HandleRequestAsync(request);
            }
            catch (Exception ex)
            {
                WriteError(request.Id, -32603, "Internal error: " + ex.ToString());
            }
        }
    }

    private static async Task HandleRequestAsync(JsonRcpRequest request)
    {
        // Notifications have no id → do not respond (e.g. notifications/initialized)
        if (!request.Id.HasValue)
        {
            // You could log here if you want, but don't write a JSON-RPC response.
            return;
        }

        switch (request.Method)
        {
            case "initialize":
                await HandleInitializeAsync(request);
                break;

            case "tools/list":
                await HandleListTools(request);
                break;

            case "tools/call":
                await HandleCallToolAsync(request);
                break;

            default:
                WriteError(request.Id, -32601, "Method not found: " + request.Method);
                break;
        }
    }

    private static Task HandleInitializeAsync(JsonRcpRequest request)
    {
        var result = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new
                {
                    // we don't send tools/listChanged notifications
                    listChanged = false,
                },
            },
            serverInfo = new { name = "AzDoErrorMcpServer", version = "0.1.0" },
            instructions = "Simple manually implemented MCP echo server.",
        };

        WriteResult(request.Id, result);
        return Task.CompletedTask;
    }

    #region Tool: list tools

    private static Task HandleListTools(JsonRcpRequest request)
    {
        var tools = new object[]
        {
            new
            {
                name = "echo",
                description = "Echoes the input text.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        text = new { type = "string", description = "The text to echo." },
                    },
                    required = new[] { "text" },
                },
            },
            new
            {
                name = "generate_pr_patch",
                description = "Fetches latest from git and generates a patch file diffing baseBranch...featureBranch.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        baseBranch = new
                        {
                            type = "string",
                            description = "The base branch name (e.g., 'main').",
                        },
                        featureBranch = new
                        {
                            type = "string",
                            description = "The feature branch name (e.g., 'feature/acqverse/tmbb').",
                        },
                        patchFileName = new
                        {
                            type = "string",
                            description = "Optional patch file name (default: 'pr.patch').",
                        },
                        remote = new
                        {
                            type = "string",
                            description = "Optional git remote (default: 'origin').",
                        },
                    },
                    required = new[] { "baseBranch", "featureBranch" },
                },
            },
        };

        var result = new { tools };

        WriteResult(request.Id, result);
        return Task.CompletedTask;
    }

    #endregion

    #region Tool: call tools

    private static async Task HandleCallToolAsync(JsonRcpRequest request)
    {
        if (request.Params == null)
        {
            WriteError(request.Id, -32602, "Missing parameters for tool call.");
            return;
        }

        var toolCall = JsonSerializer.Deserialize<ToolCallParams>(
            request.Params.Value.GetRawText(),
            _jsonOptions
        );
        if (toolCall == null)
        {
            WriteError(request.Id, -32602, "Invalid tool call parameters.");
            return;
        }

        switch (toolCall.Name)
        {
            case "echo":
                await HandleEchoToolAsync(request.Id, toolCall.Arguments);
                break;

            case "generate_pr_patch":
                await HandleGeneratePrPatchAsync(request.Id, toolCall.Arguments);
                break;

            default:
                WriteError(request.Id, -32601, "Tool not found: " + toolCall.Name);
                break;
        }
    }

    private static Task HandleEchoToolAsync(JsonElement? id, JsonElement? arguments)
    {
        if (arguments == null)
        {
            WriteError(id, -32602, "Invalid params", "Missing arguments for echo tool.");
            return Task.CompletedTask;
        }

        if (
            !arguments.Value.TryGetProperty("text", out var textElement)
            || textElement.ValueKind != JsonValueKind.String
        )
        {
            WriteError(id, -32602, "Invalid params", "Expected 'text' string in arguments.");
            return Task.CompletedTask;
        }

        var text = textElement.GetString() ?? string.Empty;

        var result = new { content = new[] { new { type = "text", text } }, isError = false };

        WriteResult(id, result);
        return Task.CompletedTask;
    }

    private static async Task HandleGeneratePrPatchAsync(JsonElement? id, JsonElement? arguments)
    {
        if (arguments == null)
        {
            WriteError(
                id,
                -32602,
                "Invalid params",
                "Missing arguments for generate_pr_patch tool."
            );
            return;
        }

        // Extract and validate parameters
        if (
            !arguments.Value.TryGetProperty("baseBranch", out var baseBranchElement)
            || baseBranchElement.ValueKind != JsonValueKind.String
        )
        {
            WriteError(id, -32602, "Invalid params", "Expected 'baseBranch' string in arguments.");
            return;
        }

        if (
            !arguments.Value.TryGetProperty("featureBranch", out var featureBranchElement)
            || featureBranchElement.ValueKind != JsonValueKind.String
        )
        {
            WriteError(
                id,
                -32602,
                "Invalid params",
                "Expected 'featureBranch' string in arguments."
            );
            return;
        }

        var baseBranch = baseBranchElement.GetString() ?? string.Empty;
        var featureBranch = featureBranchElement.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(baseBranch) || string.IsNullOrWhiteSpace(featureBranch))
        {
            WriteError(
                id,
                -32602,
                "Invalid params",
                "baseBranch and featureBranch must be non-empty strings."
            );
            return;
        }

        var remote = "origin";
        if (
            arguments.Value.TryGetProperty("remote", out var remoteElement)
            && remoteElement.ValueKind == JsonValueKind.String
        )
        {
            remote = remoteElement.GetString() ?? "origin";
        }

        var patchFileName = "pr.patch";
        if (
            arguments.Value.TryGetProperty("patchFileName", out var patchFileElement)
            && patchFileElement.ValueKind == JsonValueKind.String
        )
        {
            patchFileName = patchFileElement.GetString() ?? "pr.patch";
        }

        // Basic safety: only allow branch names containing [a-zA-Z0-9/_-]
        if (
            !IsValidBranchName(baseBranch)
            || !IsValidBranchName(featureBranch)
            || !IsValidBranchName(remote)
            || !IsValidFileName(patchFileName)
        )
        {
            WriteError(
                id,
                -32602,
                "Invalid params",
                "Branch names and patch file name contain invalid characters."
            );
            return;
        }

        var repoDir = Directory.GetCurrentDirectory();

        // Run git fetch
        var fetchResult = await RunGitCommandAsync($"fetch {remote}", repoDir);
        if (fetchResult.ExitCode != 0)
        {
            WriteError(id, -32001, "git fetch failed", fetchResult.Output);
            return;
        }

        // Run git diff
        var diffResult = await RunGitCommandAsync(
            $"diff {remote}/{baseBranch}...{remote}/{featureBranch}",
            repoDir
        );
        if (diffResult.ExitCode != 0)
        {
            WriteError(id, -32002, "git diff failed", diffResult.Output);
            return;
        }

        // Write patch file
        var patchPath = Path.Combine(repoDir, patchFileName);
        try
        {
            await File.WriteAllTextAsync(patchPath, diffResult.Output);
        }
        catch (Exception ex)
        {
            WriteError(id, -32003, "Failed to write patch file", ex.Message);
            return;
        }

        // Return success
        var resultText =
            $"Created patch file {patchFileName} comparing {remote}/{baseBranch}...{remote}/{featureBranch}. Please review this patch file using Copilot.";
        var result = new
        {
            content = new[] { new { type = "text", text = resultText } },
            isError = false,
        };

        WriteResult(id, result);
    }

    #endregion

    #region Helpers

    private static bool IsValidBranchName(string name)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9/_-]+$");
    }

    private static bool IsValidFileName(string name)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Z0-9/_.-]+$")
            && !name.Contains("..");
    }

    private static async Task<(int ExitCode, string Output)> RunGitCommandAsync(
        string arguments,
        string workingDirectory
    )
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        var process = new Process { StartInfo = psi };

        var sb = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
                sb.AppendLine(e.Data);
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
                sb.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return (process.ExitCode, sb.ToString());
    }

    private static void WriteResult(JsonElement? id, object result)
    {
        var response = new JsonRcpResponse<object>
        {
            Id = id,
            Result = JsonSerializer.SerializeToElement(result, _jsonOptions),
        };

        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
        Console.Out.WriteLine(responseJson);
        Console.Out.Flush();
    }

    private static void WriteError(JsonElement? id, int code, string message, string? data = null)
    {
        var error = new JsonRcpError
        {
            Code = code,
            Message = message,
            Data = data,
        };

        var response = new JsonRcpResponse<object> { Id = id, Error = error };

        var responseJson = JsonSerializer.Serialize(response, _jsonOptions);
        Console.Out.WriteLine(responseJson);
        Console.Out.Flush();
    }

    #endregion

    #region JSON-RPC Models


    private class JsonRcpRequest
    {
        [JsonPropertyName("jsonrpc")]
        public string? Jsonrpc { get; set; }

        [JsonPropertyName("method")]
        public string? Method { get; set; }

        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }

        // id can be string or number, so use JsonElement?
        [JsonPropertyName("id")]
        public JsonElement? Id { get; set; }
    }

    private class JsonRcpResponse<TResult>
    {
        [JsonPropertyName("jsonrpc")]
        public string Jsonrpc { get; set; } = "2.0";

        [JsonPropertyName("id")]
        public JsonElement? Id { get; set; }

        [JsonPropertyName("result")]
        public TResult? Result { get; set; }

        [JsonPropertyName("error")]
        public JsonRcpError? Error { get; set; }
    }

    private class JsonRcpError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; } = string.Empty;

        [JsonPropertyName("data")]
        public string? Data { get; set; }
    }

    private class ToolCallParams
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("arguments")]
        public JsonElement? Arguments { get; set; }
    }

    #endregion
}
