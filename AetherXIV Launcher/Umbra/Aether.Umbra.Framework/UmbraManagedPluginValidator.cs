/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Versioning;
using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal static class UmbraManagedPluginValidator
{
    public const string TargetFramework = ".NETCoreApp,Version=v10.0";
    public const string TargetPlatform = "Windows7.0";
    public const string Architecture = "x86";
    public const string Language = "CSharp";

    public static void ValidatePackage(string packageRoot, UmbraPluginManifest manifest)
    {
        string root = Path.GetFullPath(packageRoot);
        string entryPath = Path.GetFullPath(Path.Combine(root, manifest.Entry));
        ValidateEntryAssembly(entryPath);

        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(path);
            if (!string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(extension, ".exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
                if (!pe.HasMetadata)
                    throw CreateNativeBinaryError(root, path);
            }
            catch (BadImageFormatException ex)
            {
                throw CreateNativeBinaryError(root, path, ex);
            }
        }
    }

    public static void ValidateEntryAssembly(string assemblyPath)
    {
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException("Umbra plugin entry assembly was not found.", assemblyPath);

        using FileStream stream = File.Open(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        if (!pe.HasMetadata || pe.PEHeaders.CorHeader is null)
            throw new InvalidDataException("Umbra plugin entry must be a managed .NET assembly.");
        if (pe.PEHeaders.CoffHeader.Machine != Machine.I386)
            throw new InvalidDataException("Umbra plugin entry must be compatible with the x86 client process.");

        CorFlags flags = pe.PEHeaders.CorHeader.Flags;
        if (!flags.HasFlag(CorFlags.ILOnly))
            throw new InvalidDataException("Umbra plugin entry must contain managed IL only.");
        if (pe.PEHeaders.CorHeader.ManagedNativeHeaderDirectory.Size != 0)
            throw new InvalidDataException("ReadyToRun/native-image Umbra plugins are not supported.");

        MetadataReader metadata = pe.GetMetadataReader();
        string? targetFramework = ReadTargetFramework(metadata);
        if (!string.Equals(targetFramework, TargetFramework, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Umbra plugins must target net10.0-windows; assembly reports {targetFramework ?? "no target framework"}.");
        }
        string? targetPlatform = ReadAssemblyStringAttribute(metadata, nameof(TargetPlatformAttribute));
        if (!string.Equals(targetPlatform, TargetPlatform, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Umbra plugins must target net10.0-windows; assembly reports {targetPlatform ?? "no Windows target platform"}.");
        }

        string apiAssemblyName = typeof(IUmbraPlugin).Assembly.GetName().Name!;
        bool referencesApi = metadata.AssemblyReferences
            .Select(metadata.GetAssemblyReference)
            .Any(reference => string.Equals(
                metadata.GetString(reference.Name),
                apiAssemblyName,
                StringComparison.Ordinal));
        if (!referencesApi)
            throw new InvalidDataException($"Umbra plugin entry does not reference {apiAssemblyName}.");
    }

    private static string? ReadTargetFramework(MetadataReader metadata)
    {
        return ReadAssemblyStringAttribute(metadata, nameof(TargetFrameworkAttribute));
    }

    private static string? ReadAssemblyStringAttribute(MetadataReader metadata, string attributeName)
    {
        AssemblyDefinition assembly = metadata.GetAssemblyDefinition();
        foreach (CustomAttributeHandle handle in assembly.GetCustomAttributes())
        {
            CustomAttribute attribute = metadata.GetCustomAttribute(handle);
            if (!IsRuntimeVersioningAttribute(metadata, attribute.Constructor, attributeName))
                continue;

            BlobReader reader = metadata.GetBlobReader(attribute.Value);
            if (reader.ReadUInt16() != 1)
                return null;
            return reader.ReadSerializedString();
        }

        return null;
    }

    private static bool IsRuntimeVersioningAttribute(
        MetadataReader metadata,
        EntityHandle constructor,
        string attributeName)
    {
        EntityHandle parent = constructor.Kind switch
        {
            HandleKind.MemberReference => metadata.GetMemberReference((MemberReferenceHandle)constructor).Parent,
            HandleKind.MethodDefinition => metadata.GetMethodDefinition((MethodDefinitionHandle)constructor).GetDeclaringType(),
            _ => default
        };
        if (parent.IsNil)
            return false;

        return parent.Kind switch
        {
            HandleKind.TypeReference => IsRuntimeVersioningType(
                metadata,
                metadata.GetTypeReference((TypeReferenceHandle)parent),
                attributeName),
            HandleKind.TypeDefinition => IsRuntimeVersioningType(
                metadata,
                metadata.GetTypeDefinition((TypeDefinitionHandle)parent),
                attributeName),
            _ => false
        };
    }

    private static bool IsRuntimeVersioningType(
        MetadataReader metadata,
        TypeReference type,
        string attributeName) =>
        metadata.GetString(type.Namespace) == "System.Runtime.Versioning"
        && metadata.GetString(type.Name) == attributeName;

    private static bool IsRuntimeVersioningType(
        MetadataReader metadata,
        TypeDefinition type,
        string attributeName) =>
        metadata.GetString(type.Namespace) == "System.Runtime.Versioning"
        && metadata.GetString(type.Name) == attributeName;

    private static InvalidDataException CreateNativeBinaryError(
        string root,
        string path,
        Exception? inner = null) =>
        new(
            "Umbra plugins may contain managed .NET assemblies only; native binary rejected: " +
            Path.GetRelativePath(root, path),
            inner);
}
