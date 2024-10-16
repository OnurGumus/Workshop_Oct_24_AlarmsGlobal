module AlarmsGlobal.Command.API

open AlarmsGlobal.Shared.Command.Authentication
open AlarmsGlobal.Shared.Command.Subscription
open Microsoft.Extensions.Logging
open FCQRS.Model
open FCQRS.Actor
open Microsoft.Extensions.Configuration

[<Interface>]
type IAPI =
    abstract LinkIdentity: CID -> LinkIdentity
    abstract UnlinkIdentity: CID -> UnlinkIdentity
    abstract Subscribe: CID -> Subscribe
    abstract Unsubscribe: CID -> Unsubscribe
    abstract PublishEvent: CID -> PublishEvent
    abstract ActorApi: IActor

let api (env: _) =
    let config = env :> IConfiguration
    let loggerFactory = env :> ILoggerFactory
    let secretSalt = config.["config:secret-salt"]
    let actorApi = FCQRS.Actor.api config loggerFactory
    let domainApi = Command.Domain.API.api env actorApi
    let computeMd5Hash = AuthenticationHandler.computeMd5Hash secretSalt

    let userSubs cid =
        createCommandSubscription actorApi domainApi.UserFactory cid

    let subsSubs cid =
        createCommandSubscription actorApi domainApi.SubscriptionsFactory cid

    { new IAPI with

        member this.LinkIdentity cid : LinkIdentity =
            AuthenticationHandler.linkIdentity computeMd5Hash (userSubs cid.Value)

        member this.UnlinkIdentity cid : UnlinkIdentity =
            AuthenticationHandler.unlinkIdentity (userSubs cid.Value)

        member _.ActorApi = actorApi

        member this.Subscribe(cid: CID) : Subscribe =
            SubscriptionCommandHandler.subscribe (subsSubs cid.Value)

        member this.Unsubscribe(cid: CID) : Unsubscribe =
            SubscriptionCommandHandler.unsubscribe (subsSubs cid.Value)

        member this.PublishEvent cid : PublishEvent =
            SubscriptionCommandHandler.publishEvent (subsSubs cid.Value)
    }
