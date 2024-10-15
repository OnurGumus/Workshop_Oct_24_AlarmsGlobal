module SubscriptionProjection

open FSharp.Data.Sql.Common
open FCQRS.Model
open Akka.Persistence.Query
open AlarmsGlobal.Command.Domain
open SqlProvider

let handle (ctx: Sql.dataContext) eventDetails cid =
    let cid = CID.Create cid

    match eventDetails with

    | Subscriptions.Subscribed(clientId, userIdentity) ->

        failwith "not implemented"

    | Subscriptions.Unsubscribed(clientId, userIdentity) ->
        failwith "not implemented"

    | _ -> None

    