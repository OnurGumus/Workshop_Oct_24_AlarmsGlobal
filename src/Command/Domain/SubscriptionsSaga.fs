module AlarmsGlobal.Command.Domain.SubscriptionsSaga

open FCQRS
open Akkling
open Akkling.Persistence
open Akka
open Common
open Actor
open AlarmsGlobal.Shared.Model.Authentication
open Akka.Cluster.Sharding
open Common.SagaStarter
open Akka.Event
open AlarmsGlobal.Shared.Model.Subscription
open Microsoft.Extensions.Logging

type State =
    | NotStarted
    | Started of SagaStartingEvent<Event<Subscriptions.Event>>
    | SendingMessages of (GlobalEvent * UserIdentity list)
    | WaitingForCompletion
    | Completed

    interface ISerializable


type SagaData = NA

type SagaState = { Data: SagaData; State: State }

let initialState = { State = NotStarted; Data = NA }

let actorProp (env: _) (actorApi: IActor) (mediator: IActorRef<_>) (mailbox: Eventsourced<obj>) =
    let cid = (mailbox.Self.Path.Name |> SagaStarter.toCid)
    let log = mailbox.UntypedContext.GetLogger()
    let logger = env :> ILoggerFactory |> fun x -> x.CreateLogger("SubscriptionsSaga")
    let createCommand command = {
        CommandDetails = command
        CreationDate = mailbox.System.Scheduler.Now.UtcDateTime
        CorrelationId = cid
        Id = None
    }
    let continueOrAbort () =
        Subscriptions.ContinueOrAbort |> createCommand

    let complete () = Subscriptions.Complete |> createCommand

    let userActor (identity: UserIdentity) =
        let toEvent ci =
            Common.toEvent mailbox.System.Scheduler ci

        User.Actor.factory env toEvent actorApi ("User_" + identity.Value.Value)
    
    let subscriptionsActor () =
            let toEvent ci =
                Common.toEvent mailbox.System.Scheduler ci
            Subscriptions.Actor.factory env toEvent actorApi "Subscriptions"

    let apply (sagaState: SagaState) =
        match sagaState.State with
        | _ -> sagaState

    let applySideEffects (sagaState: SagaState) (startingEvent: option<SagaStartingEvent<_>>) recovering =
            match sagaState.State with
            | NotStarted -> Some(Started startingEvent.Value) // recovering is always true

            | Started e -> // almost always recovering is false
            //by default recovering should be false here until very exceptional case
                if recovering then // recovering in this case means a crash, will never in practice, but just in case
                    // we not issue a continueOrAbort command here, Case 1 or Case 2 will trigger by aggreate
                    subscriptionsActor () <!  continueOrAbort ()
                else
                    SagaStarter.cont mediator
                None
            | SendingMessages(event, users) ->
                for identity in users do
                    try
                        let cmd = User.SendMessage(event) |> createCommand
                        (userActor identity).Tell(cmd, Nobody.Instance) // fire and forget 
                    with e ->
                        log.Error(e, "Error sending mail")
                Some WaitingForCompletion // switch to WaitingForCompletion state
                // if you return None, the state will not be updated 
                // because it is fire and forget , we don't wait here just to the next one
            | WaitingForCompletion ->
                subscriptionsActor () <! complete ()
                None
            | Completed ->
                mailbox.Parent() <! Passivate(Actor.PoisonPill.Instance)
                log.Info("SubscriptionsSaga Completed")
                None
    let rec set (state: SagaState) =

        let body (msg: obj) =
            actor {
                match msg, state with
                | :? (Common.Event<Subscriptions.Event>) as { EventDetails = subsEvent }, state ->
                    match subsEvent, state with

                    | Subscriptions.Completed, _
                    | Subscriptions.Aborted, _ ->

                        let state = Completed |> StateChanged
                        return! state |> box |> Persist

                    | Subscriptions.EventPublished(globalEvent, users), _ ->

                        let state = (globalEvent, users) |> SendingMessages |> StateChanged
                        return! state |> box |> Persist

                    | _ -> return! state |> set
                | e ->
                    log.Warning("Unhandled event in global {@Event}", e)
                    return Unhandled
            }
        let wrapper = fun (s: State) -> { Data = state.Data; State = s }
        runSaga mailbox logger mediator set state applySideEffects apply wrapper body

    set initialState

let init (env: _) (actorApi: IActor) =
    (AkklingHelpers.entityFactoryFor actorApi.System shardResolver "SubscriptionsSaga"
        <| propsPersist (actorProp env actorApi (typed actorApi.Mediator))
        <| true)

let factory (env: _) toEvent actorApi entityId =
    (init env actorApi).RefFor DEFAULT_SHARD entityId