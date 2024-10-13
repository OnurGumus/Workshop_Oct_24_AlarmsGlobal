module AlarmsGlobal.Query.API

open Microsoft.Extensions.Configuration
open AlarmsGlobal.Shared.Model
open FSharp.Data.Sql.Common
open Projection
open AlarmsGlobal.Shared.Model.Authentication
open System.Text.Json.Serialization
open System.Text.Json
open AlarmsGlobal.Shared.Command.Authentication



let queryApi (config: IConfiguration) =
    failwith "Not implemented"