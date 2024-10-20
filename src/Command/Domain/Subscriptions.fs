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
    | PublishEvent of GlobalEvent
    | Complete // will come from saga
    | ContinueOrAbort // almost will never happen

type Event =
    | Subscribed of UserSubscription
    | Unsubscribed of UserSubscription
    | EventPublished of GlobalEvent * UserIdentity list
    | Completed // will go to saga, happy scenario at the and
    | Aborted // almost will never bad case.

type State = {
    Subscriptions: UserSubscription list
    LastEvents: Event<Event> list // is only used for idempotency , in this case for abort check. 
    //We check if an event with the same correlation id has been published before. If no event is found, we abort the operation.
    // Because nothing has been persisted yet and it safe to abort.
    // if an event is found, we publish it again without persisting it. And we must continue the operation.
    // Events are only stored during the of relevant Saga. Once Saga says complete, we remove them.
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
            | EventPublished _, _ -> 
               //  System.Environment.FailFast("Crash point 2") // Case 2
                 { state with LastEvents = event :: state.LastEvents } // makes sure we can control if it is persisted. Must be here if it is persisted.
            // Saga started for sure
            // but that;s all
            | Aborted , _ -> state
            | Completed, _ ->  {
                state with
                    LastEvents =
                        state.LastEvents
                        |> List.filter (fun e -> e.CorrelationId <> event.CorrelationId)
            }
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
                                    (state.Version + 1L)
                                    (Subscribed userSubscription)

                            let r =  event |> input.SendToSagaStarter // possible-crash-point 1// : Abort
                            //System.Environment.FailFast("Crash point 1") // Case 1
                            r |> Persist// possible-crash-point 2//  : Continue
                        
                        | Unsubscribe userSubscription,_ ->
                            let event = 
                                toEvent 
                                    (state.Version + 1L)
                                    (Unsubscribed userSubscription)

                            return! event |> input.SendToSagaStarter |> Persist

                        | PublishEvent globalEvent, _ ->
                            let users = state.Subscriptions |> List.filter (fun s -> s.RegionId = globalEvent.TargetRegion)
                            let event = EventPublished (globalEvent, users |> List.map (fun u -> u.Identity))
                            let outcome = toEvent (state.Version + 1L)  event |> input.SendToSagaStarter |> Persist
                            return! outcome

                         | Complete, _ ->
                            return! toEvent state.Version Completed |> input.SendToSagaStarter |> Persist

                        | ContinueOrAbort, _ -> // this happens if and only if  crashes due to any crash points. Only when saga JUST started. In practice, this will almost never happen.
                            match state.LastEvents |> Seq.tryFind (fun e -> e.CorrelationId = msg.CorrelationId) with // check case 1 or 2
                            | Some e -> e |> input.PublishEvent // case 2, already persisted so just re publish , no need to persist again
                            | None -> toEvent state.Version Aborted |> input.PublishEvent // case 1, abort

                    | _ -> return! set state
                }

            runActor logger mailbox mediator set state apply body
        
        set {
            Version = 0L;
            Subscriptions = []
            LastEvents = []
        }
    let init (env: #_) toEvent (actorApi: IActor) =
        AkklingHelpers.entityFactoryFor actorApi.System shardResolver "Subscriptions"
        <| propsPersist (actorProp env toEvent (typed actorApi.Mediator))
        <| true

    let factory (env: #_) toEvent actorApi entityId =
        (init env toEvent actorApi).RefFor DEFAULT_SHARD entityId
