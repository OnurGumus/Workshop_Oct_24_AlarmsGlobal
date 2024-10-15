module SubscriptionCommandHandler

open AlarmsGlobal.Shared.Model.Subscription
open AlarmsGlobal.Shared.Command.Subscription
open FCQRS.Model
open FCQRS.Common

open AlarmsGlobal.Command

let subscribe (createSubs) : Subscribe =
    fun id subscription ->
        async {
            let subscription = { subscription with Identity = id }

            let! subscribe =
                createSubs ("Subscriptions") (Domain.Subscriptions.Command.Subscribe subscription) (function
                    | Domain.Subscriptions.Subscribed _ -> true
                    | _ -> false)

            match subscribe with
            | {
                  EventDetails = Domain.Subscriptions.Subscribed _
                  Version = v
              } -> return Ok(Version v)
            | other -> return failwithf "unexpected event %A" other
        }

let unsubscribe (createSubs) : Unsubscribe =
    fun id subscription ->
        async {
            let subscription = { subscription with Identity = id }

            let! subscribe =
                createSubs ("Subscriptions") (Domain.Subscriptions.Command.Unsubscribe subscription) (function
                    | Domain.Subscriptions.Unsubscribed _ -> true
                    | _ -> false)

            match subscribe with
            | {
                  EventDetails = Domain.Subscriptions.Unsubscribed _
                  Version = v
              } -> return Ok(Version v)
            | other -> return failwithf "unexpected event %A" other
        }

let publishEvent (createSubs) : PublishEvent =
    fun globalEvent ->
        async {
            let! subscribe =
                createSubs ("Subscriptions") (Domain.Subscriptions.Command.PublishEvent globalEvent) (function
                    | Domain.Subscriptions.EventPublished _ -> true
                    | _ -> false)

            match subscribe with
            | {
                  EventDetails = Domain.Subscriptions.EventPublished _
                  Version = v
              } -> return Ok(Version v)
            | other -> return failwithf "unexpected event %A" other
        }
