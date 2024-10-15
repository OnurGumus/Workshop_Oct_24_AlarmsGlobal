module AlarmsGlobal.ServerInterfaces.Query

open AlarmsGlobal.Shared.Model
open AlarmsGlobal.Shared.Model.Authentication
open Command
open System.Threading.Tasks
open System.Threading
open System
open AlarmsGlobal.Shared.Model.Subscription

type AutenticationEvent =
    | AccountLinked of UserIdentity * UserClientId
    | AccountUnlinked of UserIdentity * UserClientId
    
type SubscriptionEvent =
    | Subscribed of UserSubscription
    | Unsubscribed of UserSubscription

type DataEventType =
    | AutenticationEvent of AutenticationEvent
    | SubscriptionEvent of SubscriptionEvent

[<Interface>]
type ITimeProvider =
    abstract TimeProvider: TimeProvider
