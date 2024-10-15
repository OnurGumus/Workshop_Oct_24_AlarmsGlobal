module internal AlarmsGlobal.Query.Projection

open FSharp.Data.Sql
open FSharp.Data.Sql.Common
open Microsoft.Extensions.Configuration
open AlarmsGlobal.Shared.Model
open FSharp.Data.Sql.Common
open AlarmsGlobal.Shared.Model.Authentication
open AlarmsGlobal.Shared.Command.Authentication
open FCQRS.Serialization
open FCQRS.Model
open FCQRS.Actor
open FCQRS.Common
open Akka.Streams
open Akka.Persistence.Query
open FCQRS.ModelQuery
open AlarmsGlobal.ServerInterfaces.Query
open AlarmsGlobal.Command.Domain

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
        //ContextSchemaPath=schemaLocation,
        CaseSensitivityChange=CaseSensitivityChange.ORIGINAL
     >

// QueryEvents.SqlQueryEvent
// |> Event.add (fun query -> Log.Debug("Executing SQL {query}:", query))



type CID = FCQRS.Model.CID

let handleEventWrapper (ctx: Sql.dataContext) (actorApi: IActor) (subQueue: ISourceQueue<_>) (envelop: EventEnvelope) =
    try
        //Log.Debug("Envelop:{@envelop}", envelop)
        let offsetValue = (envelop.Offset :?> Sequence).Value

        let dataEvent =
            match envelop.Event with


            | :? Event<User.Event> as { EventDetails = eventDetails; CorrelationId = cid } ->
                let cid = CID.Create cid

                match eventDetails with

                | User.UserClientIdUnlinked(clientId, userIdentity) ->
                    let encded = clientId.ToString()

                    let findRow =
                        query {
                            for c in (ctx.Main.LinkedIdentities) do
                                where (c.Identity = userIdentity.Value.Value && c.ClientId = encded)

                                take 1
                                select c
                        }
                        |> Seq.tryHead
                    //delete old row
                    match findRow with
                    | Some r -> r.Delete()
                    | None -> ()

                    let dataEvent = {
                        Type = AutenticationEvent(AccountLinked(userIdentity, clientId))
                        CID = cid
                    }

                    Some dataEvent
                //  Some {Type = AutenticationEvent(AccountUnlinked(userIdentity, clientId));CID = cid}
                | User.UserClientIdLinked(userClientId, userIdentity) ->
                    let linkedIdentity: LinkedIdentity = {
                        Identity = userIdentity
                        ClientId = userClientId
                        Type = userClientId.Type
                        Version = Version 0L
                    }

                    let row =
                        ctx.Main.LinkedIdentities.``Create(CreatedAt, Document, Type, UpdatedAt, Version)`` (
                            System.DateTime.UtcNow,
                            encodeToBytes linkedIdentity,
                            userClientId.Type,
                            System.DateTime.UtcNow,
                            0L
                        )

                    row.Identity <- userIdentity.Value.Value
                    row.ClientId <- userClientId.ToString()

                    Some {
                        Type = AutenticationEvent(AccountLinked(userIdentity, userClientId))
                        CID = cid
                    }

                | User.UserClientIdAlreadyLinked(clientId, identity) ->
                    Some {
                        Type = AutenticationEvent(AccountLinked(identity, clientId))
                        CID = cid
                    }
                | User.UserClientIdAlreadyUnlinked(clientId, identity) ->
                    Some {
                        Type = AutenticationEvent(AccountLinked(identity, clientId))
                        CID = cid
                    }

            | _ -> None

        let offset = ctx.Main.Offsets.Individuals.AlarmsGlobal
        offset.OffsetCount <- offsetValue
        ctx.SubmitUpdates()

        match (dataEvent: DataEvent<DataEventType> option) with
        | Some dataEvent -> subQueue.OfferAsync(dataEvent).Wait()
        | _ -> ()
    with ex ->
        printfn "Error: %A" ex
        actorApi.System.Terminate().Wait()
        System.Environment.Exit(-1)



let init (connectionString: string) (actorApi: IActor) query =
    let ctx = Sql.GetDataContext(connectionString)

    use conn = ctx.CreateConnection()
    conn.Open()
    let cmd = conn.CreateCommand()
    cmd.CommandText <- "PRAGMA journal_mode=WAL;"
    cmd.ExecuteNonQuery() |> ignore

    let offsetCount = ctx.Main.Offsets.Individuals.AlarmsGlobal.OffsetCount

    FCQRS.Query.init actorApi offsetCount (handleEventWrapper ctx) query
