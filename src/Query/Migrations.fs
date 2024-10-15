module Migrations

open FluentMigrator
open System
open Microsoft.Extensions.DependencyInjection
open FluentMigrator.Runner
open Microsoft.Extensions.Configuration
open System.Collections.Generic
open AlarmsGlobal.Shared.Model
open AlarmsGlobal.Shared.Model.Subscription
open FCQRS.Model
open FCQRS.Serialization

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


let regions =
    [
        "United States"
        "China"
        "Germany"
        "Japan"
        "India"
        "United Kingdom"
        "France"
        "Brazil"
        "Italy"
        "Canada"
        "Russia"
        "Mexico"
        "Australia"
        "South Korea"
        "Spain"
        "Indonesia"
        "Netherlands"
        "Turkey"
        "Saudi Arabia"
        "Switzerland"
        "Poland"
        "Taiwan"
        "Belgium"
        "Sweden"
        "Argentina"
        "Ireland"
        "Thailand"
        "Austria"
        "Israel"
        "United Arab Emirates"
        "Norway"
        "Singapore"
        "Philippines"
        "Vietnam"
        "Iran"
        "Bangladesh"
        "Malaysia"
        "Denmark"
        "Hong Kong"
        "Colombia"
        "South Africa"
        "Romania"
        "Egypt"
        "Pakistan"
        "Chile"
        "Czech Republic"
        "Finland"
        "Portugal"
        "Kazakhstan"
        "Peru"
        "Algeria"
        "Iraq"
        "New Zealand"
        "Nigeria"
        "Greece"
        "Qatar"
        "Hungary"
        "Ethiopia"
        "Ukraine"
        "Kuwait"
        "Morocco"
        "Slovakia"
        "Dominican Republic"
        "Ecuador"
        "Puerto Rico"
        "Guatemala"
        "Oman"
        "Bulgaria"
        "Kenya"
        "Venezuela"
        "Uzbekistan"
        "Costa Rica"
        "Angola"
        "Luxembourg"
        "Croatia"
        "Panama"
        "Ivory Coast"
        "Uruguay"
        "Turkmenistan"
        "Serbia"
        "Lithuania"
        "Tanzania"
        "Azerbaijan"
        "Ghana"
        "Sri Lanka"
        "DR Congo"
        "Slovenia"
        "Belarus"
        "Myanmar"
        "Uganda"
        "Tunisia"
        "Macau"
        "Jordan"
        "Cameroon"
        "Bolivia"
        "Libya"
        "Bahrain"
        "Paraguay"
        "Latvia"
        "Cambodia"
        "Nepal"
        "Estonia"
        "Honduras"
        "Senegal"
        "El Salvador"
        "Zimbabwe"
        "Cyprus"
        "Iceland"
        "Georgia"
        "Papua New Guinea"
        "Zambia"
        "Bosnia and Herzegovina"
        "Trinidad and Tobago"
        "Sudan"
        "Guinea"
        "Albania"
        "Armenia"
        "Haiti"
        "Mozambique"
        "Malta"
        "Mongolia"
        "Burkina Faso"
        "Lebanon"
        "Mali"
        "Botswana"
        "Benin"
        "Guyana"
        "Gabon"
        "Jamaica"
        "Nicaragua"
        "Niger"
        "Chad"
        "Palestine"
        "Moldova"
        "Yemen"
        "Madagascar"
        "Mauritius"
        "North Macedonia"
        "Brunei"
        "Congo"
        "Laos"
        "Afghanistan"
        "Bahamas"
        "Rwanda"
        "Kyrgyzstan"
        "Tajikistan"
        "Somalia"
        "Namibia"
        "Kosovo"
        "Malawi"
        "Equatorial Guinea"
        "Mauritania"
        "Togo"
        "Montenegro"
        "Maldives"
        "Barbados"
        "South Sudan"
        "Fiji"
        "Eswatini"
        "Liberia"
        "Sierra Leone"
        "Djibouti"
        "Suriname"
        "Aruba"
        "Andorra"
        "Belize"
        "Bhutan"
        "Burundi"
        "Central African Republic"
        "Cape Verde"
        "Gambia"
        "Saint Lucia"
        "Lesotho"
        "Seychelles"
        "Guinea-Bissau"
        "Antigua and Barbuda"
        "San Marino"
        "East Timor"
        "Solomon Islands"
        "Comoros"
        "Grenada"
        "Vanuatu"
        "Saint Kitts and Nevis"
        "Saint Vincent and the Grenadines"
        "Samoa"
        "São Tomé and Príncipe"
        "Dominica"
        "Tonga"
        "Micronesia"
        "Kiribati"
        "Palau"
        "Marshall Islands"
        "Nauru"
        "Tuvalu"
    ]
    |> List.sort
    |> List.map (fun name -> {
        RegionId = RegionId.CreateNew()
        AlrernateNames = []
        RegionType = Country
        Name = name |> ShortString.TryCreate |> forceValidate
        ParentRegionId = None
    })
    |> List.ofSeq

[<MigrationAttribute(2024_10_15_1340L)>]
type AddRegions() =
    inherit AutoReversingMigration()

    override this.Up() =
        this.Create
            .Table("Regions")
            .WithColumn("RegionId")
            .AsString()
            .PrimaryKey()
            .WithColumn("Name")
            .AsString()
            .Indexed()
            .WithColumn("Type")
            .AsString()
            .WithColumn("Document")
            .AsBinary()
        |> ignore

        for region in regions do
            let row: IDictionary<string, obj> =
                let regionList = [
                    ("RegionId", region.RegionId.Value.Value :> obj)
                    "Name", region.Name.Value
                    "Type", region.RegionType.ToString()
                    "Document", (region |> encodeToBytes) :> obj
                ]

                Map.ofSeq regionList

            this.Insert.IntoTable("Regions").Row(row) |> ignore

[<MigrationAttribute(2024_10_16_1340L)>]
type AddSubscriptions() =
    inherit AutoReversingMigration()

    override this.Up() =
        this.Create
            .Table("Subscriptions")
            .WithColumn("RegionId")
            .AsString()
            .PrimaryKey()
            .WithColumn("UserIdentity")
            .AsString()
            .PrimaryKey()
            .WithColumn("Document")
            .AsBinary()
        |> ignore
        
let updateDatabase (serviceProvider: IServiceProvider) =
    let runner = serviceProvider.GetRequiredService<IMigrationRunner>()
    runner.MigrateUp()

let resetDatabase (serviceProvider: IServiceProvider) =
    let runner = serviceProvider.GetRequiredService<IMigrationRunner>()

    if runner.HasMigrationsToApplyRollback() then
        runner.RollbackToVersion(1L)

let createServices (config: IConfiguration) =
    let connString = config.GetSection("config:connection-string").Value

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
