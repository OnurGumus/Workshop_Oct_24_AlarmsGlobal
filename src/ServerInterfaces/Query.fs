module AlarmsGlobal.ServerInterfaces.Query

open AlarmsGlobal.Shared.Model
open AlarmsGlobal.Shared.Model.Authentication
open Command
open System.Threading.Tasks
open System.Threading
open System

type AutenticationEvent =
    | AccountLinked of UserIdentity * UserClientId
    | AccountUnlinked of UserIdentity * UserClientId

type DataEventType =
    | AutenticationEvent of AutenticationEvent

[<Interface>]
type ITimeProvider =
    abstract TimeProvider: TimeProvider
