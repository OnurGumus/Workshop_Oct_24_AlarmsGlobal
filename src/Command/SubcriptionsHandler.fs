module SubscriptionsCommandHandler

open AlarmsGlobal.Shared.Command.Subscription
open FCQRS.Model
open FCQRS.Common

open AlarmsGlobal.Command

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