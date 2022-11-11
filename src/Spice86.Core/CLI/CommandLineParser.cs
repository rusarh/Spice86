﻿namespace Spice86.Core.CLI;

using CommandLine;

using Serilog;
using Serilog.Events;

using Spice86.Core.Emulator.Errors;
using Spice86.Core.Emulator.Function;
using Spice86.Core.Utils;
using Spice86.Logging;

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

/// <summary>
/// Parses the command line options to create a Configuration.<br/>
/// Displays help when configuration could not be parsed.
/// </summary>
public class CommandLineParser {
    private static readonly ILogger _logger = Serilogger.Logger.ForContext<CommandLineParser>();

    public static Configuration ParseCommandLine(string[] args) {
        ParserResult<Configuration> result = Parser.Default.ParseArguments<Configuration>(args)
            .WithNotParsed((e) => _logger.Error("{@Errors}", e));
        return result.MapResult(initialConfig => {
            if (initialConfig.InitializeDOS is null) {
                initialConfig.InitializeDOS = true;
            }
            initialConfig.Exe = ParseExePath(initialConfig.Exe);
            initialConfig.CDrive ??= Path.GetDirectoryName(initialConfig.Exe);
            initialConfig.ExpectedChecksumValue = string.IsNullOrWhiteSpace(initialConfig.ExpectedChecksum) ? Array.Empty<byte>() : ConvertUtils.HexToByteArray(initialConfig.ExpectedChecksum);
            initialConfig.OverrideSupplier = ParseFunctionInformationSupplierClassName(initialConfig);
            if (initialConfig.WarningLogs) {
                Serilogger.LogLevelSwitch.MinimumLevel = LogEventLevel.Warning;
            }
            if (initialConfig.VerboseLogs) {
                Serilogger.LogLevelSwitch.MinimumLevel = LogEventLevel.Verbose;
            }
            return initialConfig;
        }, error => {
            var message = "Unparseable command line";
            var exception = new UnreachableException(message);
            exception.Data.Add("Error", error);
            Environment.FailFast(message, exception);
            throw exception;
        });
    }

    private static string? ParseExePath(string? exePath) {
        string? unixPathValue = exePath?.Replace('\\', '/');
        if (File.Exists(exePath)) {
            return new FileInfo(exePath).FullName;
        }
        return unixPathValue;
    }

    private static IOverrideSupplier? ParseFunctionInformationSupplierClassName(Configuration configuration) {
        string? supplierClassName = configuration.OverrideSupplierClassName;
        if (supplierClassName == null) {
            return null;
        }

        try {
            Type? supplierClass = Type.GetType(supplierClassName);
            if (!typeof(IOverrideSupplier).IsAssignableFrom(supplierClass)) {
                string error = $"Provided class {supplierClassName} does not implement the {typeof(IOverrideSupplier).FullName} interface ";
                throw new UnrecoverableException(error);
            }

            return (IOverrideSupplier?)Activator.CreateInstance(supplierClass);
        } catch (MethodAccessException exception) {
            exception.Demystify();
            throw new UnrecoverableException($"Could not load provided class {supplierClassName}", exception);
        } catch (TargetInvocationException exception) {
            exception.Demystify();
            throw new UnrecoverableException($"Could not instantiate provided class {supplierClassName}", exception);
        } catch (NotSupportedException exception) {
            exception.Demystify();
            throw new UnrecoverableException($"Could not instantiate provided class {supplierClassName}", exception);
        } catch (ArgumentException exception) {
            exception.Demystify();
            throw new UnrecoverableException($"Could not instantiate provided class {supplierClassName}", exception);
        } catch (MemberAccessException exception) {
            exception.Demystify();
            throw new UnrecoverableException($"Could not instantiate provided class {supplierClassName}", exception);
        } catch (TypeLoadException exception) {
            exception.Demystify();
            throw new UnrecoverableException($"Could not instantiate provided class {supplierClassName}", exception);
        }
    }
}