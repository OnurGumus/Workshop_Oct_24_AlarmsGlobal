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

    | Subscriptions.Subscribed(identity, regionId) ->

        let row =
            ctx.Main.Subscriptions.``Create(Document)`` (encodeToBytes (regionId, regionId))

        row.RegionId <- regionId.Value.Value
        row.UserIdentity <- identity.Value.Value

        let dataEvent: DataEvent<DataEventType> = {
            Type = SubscriptionEvent(Subscribed(identity, regionId))
            CID = cid
        }

        Some dataEvent

    | Subscriptions.Unsubscribed(clientId, userIdentity) -> failwith "not implemented"
    | _ -> None
