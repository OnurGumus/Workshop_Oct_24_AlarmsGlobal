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
    | Subscriptions.Subscribed(userSubscription) ->
        let row = ctx.Main.Subscriptions.``Create(Document)``(encodeToBytes userSubscription)
        row.RegionId <- userSubscription.RegionId.Value.Value
        row.UserIdentity <- userSubscription.Identity.Value.Value

        let dataEvent = {
            Type = SubscriptionEvent(Subscribed(userSubscription))
            CID = cid
        }
        Some dataEvent
    | Subscriptions.Unsubscribed(subscription) -> failwith "not implemented"
    | _ -> None


        