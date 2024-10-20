module internal SqlProvider

open FSharp.Data.Sql
open FSharp.Data.Sql.Common

// 1. Either create the serializer options from the F# options...
[<Literal>]
let resolutionPath = __SOURCE_DIRECTORY__ + @"/libs"

[<Literal>]
let schemaLocation = __SOURCE_DIRECTORY__ + @"/../Server/Database/Schema.sqlite"
#if DEBUG

[<Literal>]
let connectionString =
    @"Data Source=" + __SOURCE_DIRECTORY__ + @"/../Server/Database/AlarmsGlobal.db;"

#else

[<Literal>]
let connectionString = @"Data Source=" + @"Database/AlarmsGlobal.db;"

#endif


type Sql =
    SqlDataProvider<
        DatabaseProviderTypes.SQLITE,
        SQLiteLibrary=SQLiteLibrary.MicrosoftDataSqlite,
        ConnectionString=connectionString,
        ResolutionPath=resolutionPath,
        ContextSchemaPath=schemaLocation,
        CaseSensitivityChange=CaseSensitivityChange.ORIGINAL
     >

// QueryEvents.SqlQueryEvent
// |> Event.add (fun query -> Log.Debug("Executing SQL {query}:", query))
