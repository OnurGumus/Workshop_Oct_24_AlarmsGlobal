module internal AlarmsGlobal.Query.Projection

open FSharp.Data.Sql.Common
open FCQRS.Actor
open FCQRS.Common
open Akka.Streams
open Akka.Persistence.Query
open FCQRS.ModelQuery
open AlarmsGlobal.ServerInterfaces.Query
open AlarmsGlobal.Command.Domain
open SqlProvider

type CID = FCQRS.Model.CID

let handleEventWrapper (ctx: Sql.dataContext) (actorApi: IActor) (subQueue: ISourceQueue<_>) (envelop: EventEnvelope) =
    try
        //Log.Debug("Envelop:{@envelop}", envelop)
        let offsetValue = (envelop.Offset :?> Sequence).Value

        let dataEvent =
            match envelop.Event with


            | :? Event<User.Event> as { EventDetails = eventDetails; CorrelationId = cid } ->
                UserProjection.handle ctx eventDetails cid
            | :? Event<Subscriptions.Event> as { EventDetails = eventDetails; CorrelationId = cid } ->
                SubscriptionProjection.handle ctx eventDetails cid
                
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
