module AlarmsGlobal.Query.API

open Microsoft.Extensions.Configuration
open AlarmsGlobal.Shared.Model
open FSharp.Data.Sql.Common
open Projection
open AlarmsGlobal.Shared.Model.Authentication
open System.Text.Json.Serialization
open System.Text.Json
open AlarmsGlobal.Shared.Command.Authentication
open FCQRS.Serialization



let queryApi (config: IConfiguration) =

    let connString = config.GetSection("config:connection-string").Value

    let query(ty:System.Type, filter, orderby, orderbydesc, thenby, thenbydesc, take : int option, skip, (cacheKey: string option)) 
            : Async<obj seq> =

                let ctx = Sql.GetDataContext(connString)

                let rec eval (t) =
                    match t with
                    | Equal(s, n) -> <@@ fun (x: SqlEntity) -> x.GetColumn(s) = n @@>
                    | NotEqual(s, n) -> <@@ fun (x: SqlEntity) -> x.GetColumn(s) <> n @@>
                    | Greater(s, n) -> <@@ fun (x: SqlEntity) -> x.GetColumn(s) > n @@>
                    | GreaterOrEqual(s, n) -> <@@ fun (x: SqlEntity) -> x.GetColumn(s) >= n @@>
                    | Smaller(s, n) -> <@@ fun (x: SqlEntity) -> x.GetColumn(s) < n @@>
                    | SmallerOrEqual(s, n) -> <@@ fun (x: SqlEntity) -> x.GetColumn(s) <= n @@>
                    | And(t1, t2) -> <@@ fun (x: SqlEntity) -> (%%eval t1) x && (%%eval t2) x @@>
                    | Or(t1, t2) -> <@@ fun (x: SqlEntity) -> (%%eval t1) x || (%%eval t2) x @@>
                    | Not(t0) -> <@@ fun (x: SqlEntity) -> not ((%%eval t0) x) @@>

                let augment db = FCQRS.SQLProvider.Query.augment filter eval orderby orderbydesc thenby thenbydesc take skip db

                let res : seq<obj> =
                
                    if ty = typeof<LinkedIdentity> then
                        let q =
                            query {
                                for c in ctx.Main.LinkedIdentities do
                                    select c
                            }

                        augment <@ q @>
                        |> Seq.map (fun x ->x.Document |> decodeFromBytes<LinkedIdentity> :> obj)
                            

                    else
                        failwith "not implemented"

                

                async { return res  }
    
    //Projection.init connString actorApi query
    query


