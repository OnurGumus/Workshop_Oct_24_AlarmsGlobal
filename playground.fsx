#load  "main.fsx"


open AlarmsGlobal.Query
open System
open System.IO
open Microsoft.Extensions.Logging
open Microsoft.Extensions.DependencyInjection
open Giraffe.SerilogExtensions
open Microsoft.Extensions.Configuration
open Serilog
open Hocon.Extensions.Configuration
open AlarmsGlobal.Shared.Model
open AlarmsGlobal.Shared.Model.Authentication
open AlarmsGlobal.ServerInterfaces.Command
open AlarmsGlobal.ServerInterfaces.Query
open FsToolkit.ErrorHandling
open Main
open FCQRS.Model

let userClientId = Email.TryCreate("test@example.com") |> forceValidate |> Email

let userIdentity = UserIdentity.CreateNew()

commandApi.LinkIdentity (CID.CreateNew()) (Some userIdentity) userClientId
|> Async.Ignore
|> Async.RunSynchronously

commandApi.UnlinkIdentity (CID.CreateNew()) ( userIdentity) userClientId
|> Async.Ignore
|> Async.RunSynchronously