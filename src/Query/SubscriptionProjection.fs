module SubscriptionProjection

open FSharp.Data.Sql.Common
open FCQRS.Model
open Akka.Persistence.Query
open AlarmsGlobal.Command.Domain
open SqlProvider
open FCQRS.Serialization
open AlarmsGlobal.ServerInterfaces.Query
open FCQRS.ModelQuery

let handle (ctx: Sql.dataContext) eventDetails cid =
    let cid = CID.Create cid

    match eventDetails with

    | Subscriptions.Subscribed(subscription) ->

        let row =
            ctx.Main.Subscriptions.``Create(Document)`` (encodeToBytes (subscription))

        row.RegionId <- subscription.RegionId.Value.Value
        row.UserIdentity <- subscription.Identity.Value.Value

        let dataEvent: DataEvent<DataEventType> = {
            Type = SubscriptionEvent(Subscribed(subscription))
            CID = cid
        }

        Some dataEvent

    | Subscriptions.Unsubscribed(subscription) -> failwith "not implemented"
    | _ -> None
