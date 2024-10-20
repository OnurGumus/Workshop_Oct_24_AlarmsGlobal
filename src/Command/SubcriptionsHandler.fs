module SubscriptionsCommandHandler

open AlarmsGlobal.Shared.Command.Subscription
open FCQRS.Model
open FCQRS.Common

open AlarmsGlobal.Command

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
let subscribe (createSubs) : Subscribe =
    fun userSubscription ->
        async{
            let! subscribe =
                createSubs "Subscriptions" (Domain.Subscriptions.Command.Subscribe (userSubscription))(function 
                    | Domain.Subscriptions.Subscribed _ -> true
                    | _ -> false)
        
            match subscribe with
            | {
                EventDetails = Domain.Subscriptions.Subscribed _
                Version = v
             } -> return Ok(Version v)
            | _ -> return failwithf "unexpected event %A" subscribe
        }

let unsubscribe (createSubs) : Unsubscribe =
    fun userSubscription ->
        async{
            let! subscribe =
                createSubs "Subscriptions" (Domain.Subscriptions.Command.Unsubscribe (userSubscription))(function 
                    | Domain.Subscriptions.Unsubscribed _ -> true
                    | _ -> false)
        
            match subscribe with
            | {
                EventDetails = Domain.Subscriptions.Unsubscribed _
                Version = v
             } -> return Ok(Version v)
            | _ -> return failwithf "unexpected event %A" subscribe
        }