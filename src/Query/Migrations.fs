module Migrations

open FluentMigrator
open System
open Microsoft.Extensions.DependencyInjection
open FluentMigrator.Runner
open Microsoft.Extensions.Configuration
open System.Collections.Generic

[<MigrationAttribute(1L)>]
type Zero() =
    inherit Migration()

    override this.Up() = ()

    override this.Down() = ()

[<MigrationAttribute(2L)>]
type One() =
    inherit Migration()

    override this.Up() = ()

    override this.Down() =
        try
            if this.Schema.Table("snapshot").Exists() then
                // clean up akka stuff
                this.Execute.Sql("DELETE FROM snapshot")
                this.Execute.Sql("DELETE FROM JOURNAL")
                this.Execute.Sql("DELETE FROM SQLITE_SEQUENCE")
                this.Execute.Sql("DELETE FROM TAGS")
        with _ ->
            ()
    


[<MigrationAttribute(2024_10_10_2102L)>]
type AddLinkedIdentityTable() =
    inherit AutoReversingMigration()

    override this.Up() =
        this.Create
            .Table("LinkedIdentities")
            .WithColumn("ClientId")
            .AsString()
            .PrimaryKey()
            .WithColumn("Identity")
            .AsString()
            .PrimaryKey()
            .WithColumn("Type")
            .AsString()
            .NotNullable()
            .WithColumn("Document")
            .AsBinary()
            .NotNullable()
            .WithColumn("Version")
            .AsInt64()
            .NotNullable()
            .WithColumn("CreatedAt")
            .AsDateTime()
            .NotNullable()
            .Indexed()
            .WithColumn("UpdatedAt")
            .AsDateTime()
            .NotNullable()
            .Indexed()
        |> ignore


[<MigrationAttribute(2024_10_13_2101L)>]
type AddOffsetsTable() =
    inherit AutoReversingMigration()

    override this.Up() =
        this.Create
            .Table("Offsets")
            .WithColumn("OffsetName")
            .AsString()
            .PrimaryKey()
            .WithColumn("OffsetCount")
            .AsInt64()
            .NotNullable()
            .WithDefaultValue(0)
        |> ignore

        let dict: IDictionary<string, obj> = Dictionary()
        dict.Add("OffsetName", "AlarmsGlobal")
        dict.Add("OffsetCount", 0L)

        this.Insert.IntoTable("Offsets").Row(dict) |> ignore
        



let updateDatabase (serviceProvider: IServiceProvider) =
    let runner = serviceProvider.GetRequiredService<IMigrationRunner>()
    runner.MigrateUp()

let resetDatabase (serviceProvider: IServiceProvider) =
    let runner = serviceProvider.GetRequiredService<IMigrationRunner>()

    if runner.HasMigrationsToApplyRollback() then
        runner.RollbackToVersion(1L)

let createServices (config: IConfiguration) =
    let connString =
        config.GetSection("config:connection-string").Value

    ServiceCollection()
        .AddFluentMigratorCore()
        .ConfigureRunner(fun rb ->
            rb
                .AddSQLite()
                .WithGlobalConnectionString(connString)
                .ScanIn(typeof<Zero>.Assembly)
                .For.Migrations()
            |> ignore)
        .AddLogging(fun lb -> lb.AddFluentMigratorConsole() |> ignore)
        .BuildServiceProvider(false)

let init (env: _) =
    let config = env :> IConfiguration
    use serviceProvider = createServices config
    use scope = serviceProvider.CreateScope()
    updateDatabase scope.ServiceProvider

let reset (env: _) =
    let config = env :> IConfiguration
    use serviceProvider = createServices config
    use scope = serviceProvider.CreateScope()
    resetDatabase scope.ServiceProvider
    init env
