namespace Innago.Shared.HealthChecks.Npgsql;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

using Innago.Shared.TryHelpers;

using Microsoft.Extensions.Logging;

// Intentional copy: see DESIGN.md §Package Structure

[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
internal partial class VaultSecretsJsonContext : JsonSerializerContext;

internal readonly record struct VaultSecrets(Dictionary<string, string> Values);

internal static class VaultSecretsReader
{
    internal static Result<VaultSecrets?> TryRead(string path, ILogger logger)
    {
        try
        {
            string json = File.ReadAllText(path);
            var dict = JsonSerializer.Deserialize(json, VaultSecretsJsonContext.Default.DictionaryStringString);
            return dict is null
                ? new Result<VaultSecrets?>((VaultSecrets?)null)
                : new Result<VaultSecrets?>(new VaultSecrets(dict));
        }
        catch (FileNotFoundException)
        {
            return new Result<VaultSecrets?>((VaultSecrets?)null);
        }
        catch (DirectoryNotFoundException)
        {
            return new Result<VaultSecrets?>((VaultSecrets?)null);
        }
        catch (Exception ex)
        {
            LogMessages.VaultFileReadError(logger, path, ex);
            return new Result<VaultSecrets?>(ex);
        }
    }
}
