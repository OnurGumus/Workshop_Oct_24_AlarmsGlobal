module AlarmsGlobal.Shared.Query

open AlarmsGlobal.Shared.Model.Authentication
open AlarmsGlobal.Shared.Model.Subscription
open System

module Subscription =

    type GetRegions = unit -> Async<Result<Region list, string list>>
    type GetSubscriptions = Guid -> Async<Result<UserSubscription list, string list>>
    type GetUserClientIds = Guid -> Async<Result<UserClientId list, string list>>