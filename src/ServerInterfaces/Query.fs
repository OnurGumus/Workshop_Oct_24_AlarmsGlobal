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


// type DataEvent = { Type: DataEventType; CID: CID }


// [<Interface>]
// type IQuery =
//     abstract Query<'t> :
//         ?filter: Predicate *
//         ?orderby: string *
//         ?orderbydesc: string *
//         ?thenby: string *
//         ?thenbydesc: string *
//         ?take: int *
//         ?skip: int *
//         ?cacheKey: string ->
//             list<'t> Async

//     abstract Subscribe: callback:(DataEvent -> unit) * CancellationToken -> unit
//     abstract Subscribe: filter:(DataEvent -> bool) * numberOfEvents:int * callback:(DataEvent -> unit) * CancellationToken -> Async<unit>

[<Interface>]
type ITimeProvider =
    abstract TimeProvider: TimeProvider
