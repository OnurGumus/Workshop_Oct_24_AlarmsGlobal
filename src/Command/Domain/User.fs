module AlarmsGlobal.Command.Domain.User

open AlarmsGlobal.Shared.Model
open Authentication
open FCQRS.Common
open FCQRS
open AlarmsGlobal.Shared.Model.Subscription

type Event =
    | UserClientIdLinked of UserClientId * UserIdentity
    | UserClientIdAlreadyLinked of UserClientId * UserIdentity
    | UserClientIdUnlinked of UserClientId * UserIdentity
    | UserClientIdAlreadyUnlinked of UserClientId * UserIdentity

type Command =
    | LinkUserClientId of UserClientId * UserIdentity
    | UnlinkUserClientId of UserClientId
    | SendMessage of GlobalEvent


type State = {
    Version: int64
    UserIdentity: UserIdentity option
    UserClientIds: UserClientId list
} with

    interface ISerializable

module internal Actor =
    open Akkling
    open Akka.Cluster.Tools.PublishSubscribe
    open Akkling.Persistence
    open Actor
    open Akkling
    open Microsoft.Extensions.Configuration
    open Microsoft.Extensions.Logging
    open Akka

    type ToEvent<'Event> = string option -> string -> int64 -> 'Event -> Event<'Event>

    let actorProp (env: _) (toEvent: ToEvent<Event>) (mediator: IActorRef<Publish>) (mailbox: Eventsourced<obj>) =

        let config = env :> IConfiguration
        let loggerFactory = env :> ILoggerFactory
        let logger = loggerFactory.CreateLogger("UserActor")

        let apply (event: Event<_>) (_: State as state) =
            match event.EventDetails, state with

            | UserClientIdUnlinked(userClientId, identity), _ -> {
                state with
                    UserClientIds = state.UserClientIds |> List.filter (fun x -> x <> userClientId)
                    UserIdentity = Some identity
              }
            | UserClientIdLinked(userClientId, identity), _ -> {
                state with
                    UserClientIds = userClientId :: state.UserClientIds
                    UserIdentity = Some identity
              }
            | UserClientIdAlreadyUnlinked _, _
            | UserClientIdAlreadyLinked _, _ -> state
            |> fun state -> { state with Version = event.Version }

        let rec set (state: State) =
            let body (bodyInput: BodyInput<Event>) =
                let msg = bodyInput.Message

                actor {
                    match msg, state with

                    | :? Persistence.RecoveryCompleted, _ -> return! state |> set

                    | :? (Common.Command<Command>) as msg, _ ->
                        let toEvent = toEvent (msg.Id) msg.CorrelationId

                        match msg.CommandDetails, state with
                        | SendMessage(globalEvent), _ ->
                            logger.LogInformation("Sending message: {globalEvent}", globalEvent)
                            return! set state
                        | UnlinkUserClientId(userClientId), _ ->
                            if state.UserClientIds |> List.exists (fun x -> x = userClientId) then
                                let event =
                                    toEvent
                                        (state.Version + 1L)
                                        (UserClientIdUnlinked(userClientId, state.UserIdentity.Value))

                                return! event |> bodyInput.SendToSagaStarter |> Persist
                            else
                                let event =
                                    toEvent
                                        (state.Version)
                                        (UserClientIdAlreadyUnlinked(userClientId, state.UserIdentity.Value))

                                return! event |> bodyInput.SendToSagaStarter |> Persist

                        | LinkUserClientId(userClientId, identity), _ ->
                            if state.UserClientIds |> List.exists (fun x -> x = userClientId) then
                                let event =
                                    toEvent state.Version (UserClientIdAlreadyLinked(userClientId, identity))

                                return! event |> bodyInput.SendToSagaStarter |> Persist
                            else
                                let event =
                                    toEvent (state.Version + 1L) (UserClientIdLinked(userClientId, identity))

                                return! event |> bodyInput.SendToSagaStarter |> Persist

                    | _ ->
                        bodyInput.Log.LogWarning("Unhandled message: {msg}", msg)
                        return Unhandled
                }

            runActor logger mailbox mediator set state apply body

        set { Version = 0L; UserClientIds = []; UserIdentity = None }


    let init (env: _) toEvent (actorApi: IActor) =
        AkklingHelpers.entityFactoryFor actorApi.System shardResolver "User"
        <| propsPersist (actorProp env toEvent (typed actorApi.Mediator))
        <| true

    let factory (env: #_) toEvent actorApi entityId =
        (init env toEvent actorApi).RefFor DEFAULT_SHARD entityId
