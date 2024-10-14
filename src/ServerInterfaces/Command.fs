module AlarmsGlobal.ServerInterfaces.Command

open AlarmsGlobal.Shared.Command.Authentication
open FCQRS.Model


[<Interface>]
type IAuthentication =
    abstract LinkIdentity: CID -> LinkIdentity
    abstract UnlinkIdentity: CID -> UnlinkIdentity
        
        