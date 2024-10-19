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


type Command =
    | Subscribe of UserSubscription
    | Unsubscribe of UserSubscription

type Event =
    | Subscribed of UserSubscription
    | Unsubscribed of UserSubscription

type State = {
    Subscriptions: UserSubscription list
    Version: int64
} with
    interface ISerializable


module internal Actor =
    let actorProp
        (env: _)
        (toEvent: string option -> string -> int64 -> Event -> _)
        (mediator: IActorRef<Publish>)
        (mailbox: Eventsourced<obj>)
        =
        let config = env :> IConfiguration
        let logger = env :> ILoggerFactory |> fun f -> f.CreateLogger("SubscriptionsActor")

        let apply (event: Event<_>) (state: State) =
            match event.EventDetails, state with
            | Subscribed sub, _ -> { state with Subscriptions = sub :: state.Subscriptions }
            | Unsubscribed sub, _ -> { state with Subscriptions = state.Subscriptions |> List.filter (fun s -> s <> sub) } 
            |> fun state -> { state with Version = event.Version }

        let rec set (state: State) =
            let body (input: BodyInput<Event>) =
                let msg = input.Message
                actor{
                    match msg, state with
                    | :? Persistence.RecoveryCompleted, _ -> return! state |> set
                    | :? (Common.Command<Command>) as msg, _ ->
                        let toEvent = toEvent (msg.Id) msg.CorrelationId

                        match msg.CommandDetails, state with
                        | Subscribe userSubscription,_ ->
                            let event = 
                                toEvent 
                                    (state.Version)
                                    (Subscribed userSubscription)

                            return! event |> input.SendToSagaStarter |> Persist
                        
                        | Unsubscribe userSubscription,_ ->
                            let event = 
                                toEvent 
                                    (state.Version)
                                    (Unsubscribed userSubscription)

                            return! event |> input.SendToSagaStarter |> Persist
                    | _ -> return! set state
                }

            runActor logger mailbox mediator set state apply body
        
        set {
            Version = 0L;
            Subscriptions = []
        }
    let init (env: #_) toEvent (actorApi: IActor) =
        AkklingHelpers.entityFactoryFor actorApi.System shardResolver "Subscriptions"
        <| propsPersist (actorProp env toEvent (typed actorApi.Mediator))
        <| true

    let factory (env: #_) toEvent actorApi entityId =
        (init env toEvent actorApi).RefFor DEFAULT_SHARD entityId
