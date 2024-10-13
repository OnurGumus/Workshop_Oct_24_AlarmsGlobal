module AlarmsGlobal.ServerInterfaces.Command

open AlarmsGlobal.Shared.Command.Authentication
open AlarmsGlobal.Shared.Model
open System

type CID =
    | CID of ShortString

    member this.Value: string = let (CID v) = this in v.Value

    static member CreateNew() =
        Guid.NewGuid().ToString() |> ShortString.TryCreate |> forceValidate |> CID

    static member Create(s: string) =
        let s = if (s.Contains "~") then s.Split("~")[1] else s
        s |> ShortString.TryCreate |> forceValidate |> CID


[<Interface>]
type IAuthentication =
    abstract LinkIdentity: CID -> LinkIdentity
    abstract UnlinkIdentity: CID -> UnlinkIdentity
        
        