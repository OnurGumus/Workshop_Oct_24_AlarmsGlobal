module Serilog

open Serilog
open Serilog.Sinks.SystemConsole.Themes
open Serilog.Formatting.Compact
open System
open Serilog.Core
open Serilog.Events
open Microsoft.Extensions.Hosting

let bootstrapLogger () =
    Log.Logger <-
        LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(theme = AnsiConsoleTheme.Literate, applyThemeToRedirectedOutput = true)
            .WriteTo.File(
                new CompactJsonFormatter(),
                "logs/log_boot_strapper_json_.txt",
                rollingInterval = RollingInterval.Day
            )
            .WriteTo.Seq("http://host.docker.internal:5341")
            .CreateBootstrapLogger()

let configureLogging (ctx: HostBuilderContext) (services: IServiceProvider) (loggerConfiguration: LoggerConfiguration) =
    //let apiKey = ctx.Configuration["config:serilog-api-key"]
    let levelSwitch = LoggingLevelSwitch()

    loggerConfiguration.MinimumLevel
        .Override("Microsoft", LogEventLevel.Warning)
        .MinimumLevel.Override("Giraffe", LogEventLevel.Warning)
        .MinimumLevel.ControlledBy(levelSwitch)
        .Enrich.WithProperty("Application", "AlarmsGlobal")
        .Enrich.FromLogContext()
        .Destructure.FSharpTypes()
        .WriteTo.File(new CompactJsonFormatter(), "logs/log_json_.txt", rollingInterval = RollingInterval.Day)
        .WriteTo.Console(theme = AnsiConsoleTheme.Literate, applyThemeToRedirectedOutput = true)
// #if DEBUG
//         .WriteTo.Seq("http://host.docker.internal:5341")
// #else
//         .WriteTo.Seq("http://my-seq.default:5341", apiKey = apiKey, controlLevelSwitch = levelSwitch)
// #endif
    |> ignore
