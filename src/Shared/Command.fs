module AlarmsGlobal.Shared.Command
open AlarmsGlobal.Shared.Model
open AlarmsGlobal.Shared.Model
open AlarmsGlobal.Shared.Model.Authentication
open FCQRS.Model

module Authentication =
    type LinkIdentity =  UserIdentity option ->  UserClientId -> Async<Result<Version, string list>>
    type UnlinkIdentity =  UserIdentity  ->  UserClientId -> Async<Result<Version, string list>>
