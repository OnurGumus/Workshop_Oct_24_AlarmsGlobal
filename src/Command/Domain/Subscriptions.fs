module AlarmsGlobal.Command.Domain.Subscriptions

open FCQRS
open Akkling
open Akkling.Persistence
open Akka
open Common
open Akka.Cluster.Tools.PublishSubscribe
open Actor
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging
open AlarmsGlobal.Shared.Model.Subscription
open AlarmsGlobal.Shared.Model.Authentication


type Event =
    | Subscribed of UserSubscription
    | Unsubscribed of UserSubscription
    | EventPublished of GlobalEvent


type Command =
    | Subscribe of UserSubscription
    | Unsubscribe of UserSubscription
    | PublishEvent of GlobalEvent


type State = {
    Subscriptions: UserSubscription list
    Version: int64
} with
    interface ISerializable

module Actor =
    open FCQRS.ModelQuery
   
    let actorProp
        (env: _)
        (toEvent: string option -> string -> int64 -> Event -> _)
        (mediator: IActorRef<Publish>)
        (mailbox: Eventsourced<obj>)
        =
        let config = env :> IConfiguration
        let logger = env :> ILoggerFactory  |> fun f -> f.CreateLogger("SubscriptionsActor")

        let apply (event: Event<_>) (_: State as state) =

            match event.EventDetails, state with
            | _ -> state
           
            |> fun state -> { state with Version = event.Version }

        let rec set (state: State) =
            let body (input: BodyInput<Event>) =
                let msg = input.Message

                actor {
                    match msg, state with

                    | :? Persistence.RecoveryCompleted, _ ->
                            return! set state

                    | :? (Common.Command<Command>) as msg, _ ->
                        let query = env :> IQuery<_>
                        let regions = query.Query<Region>(cacheKey = "regions") |> Async.RunSynchronously

                        let ci = msg.CorrelationId
                        let commandDetails = msg.CommandDetails
                        let v = state.Version
                        let toEvent = toEvent (msg.Id)

                        match commandDetails, state with

                        | Subscribe subs, _ ->
                            let subscribeEvent, v = Subscribed subs, (v + 1L)
                            let outcome = toEvent ci v subscribeEvent |> input.SendToSagaStarter |> Persist
                            return! outcome

                        | Unsubscribe subs, _ ->
                            let subscribeEvent, v = Unsubscribed subs, (v + 1L)
                            let outcome = toEvent ci v subscribeEvent |> input.SendToSagaStarter |> Persist
                            return! outcome

                        | PublishEvent globalEvent, _ ->
                            let outcome = toEvent ci v (EventPublished globalEvent) |> input.SendToSagaStarter |> Persist
                            return! outcome
                    | _ ->
                        input.Log.LogDebug("Unhandled Message {@MSG}", box msg)
                        return Unhandled
                }

            runActor logger mailbox mediator set state apply body

        set {
            Subscriptions = []
            Version = 0L
        }

    let  init (env: #_) toEvent (actorApi: IActor) =
        AkklingHelpers.entityFactoryFor actorApi.System shardResolver "Subscriptions"
        <| propsPersist (actorProp env toEvent (typed actorApi.Mediator))
        <| true

    let  factory (env: #_) toEvent actorApi entityId =
        (init env toEvent actorApi).RefFor DEFAULT_SHARD entityId