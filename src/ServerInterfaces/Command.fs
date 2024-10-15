module AlarmsGlobal.ServerInterfaces.Command

open AlarmsGlobal.Shared.Command.Authentication
open AlarmsGlobal.Shared.Command.Subscription
open FCQRS.Model

[<Interface>]
type IAuthentication =
    abstract LinkIdentity: CID -> LinkIdentity
    abstract UnlinkIdentity: CID -> UnlinkIdentity
        
[<Interface>]
type ISubscription =
    abstract Subscribe: CID -> Subscribe
    abstract Unsubscribe: CID -> Unsubscribe
    abstract PublishEvent: CID -> PublishEvent