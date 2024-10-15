module AlarmsGlobal.ServerInterfaces.Query

open AlarmsGlobal.Shared.Model
open AlarmsGlobal.Shared.Model.Authentication
open Command
open System.Threading.Tasks
open System.Threading
open System

type AutenticationEvent =
    | AccountLinked of UserIdentity * UserClientId
    | AccountUnlinked of UserIdentity * UserClientId
    
type SubscriptionEvent =
    | Subscribed of UserClientId * UserIdentity
    | Unsubscribed of UserClientId * UserIdentity

type DataEventType =
    | AutenticationEvent of AutenticationEvent
    | SubscriptionEvent of SubscriptionEvent

[<Interface>]
type ITimeProvider =
    abstract TimeProvider: TimeProvider
