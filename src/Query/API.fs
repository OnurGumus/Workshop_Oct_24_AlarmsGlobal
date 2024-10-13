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



module Command =
    open System.Security.Cryptography
    open System.Text
    open FSharp.Data.Sql
    let  computeMd5Hash salt (input: string) =
        using (MD5.Create()) (fun md5Hash ->
            let input = input + salt
            // Compute the hash of the input string
            let bytes = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input))

            // Convert the byte array to a hexadecimal string
            let builder = StringBuilder(32)
            bytes |> Array.iter (fun byte -> builder.Append(byte.ToString("x2")) |> ignore)
            builder.ToString())

    let api (config: IConfiguration) =
            let connString = config.GetSection("config:connection-string").Value

            let linkIdentity : LinkIdentity  = 
                fun identityOption userClientId ->
                let userIdentity =
                    identityOption
                    |> Option.defaultWith (fun () ->
                        let clientIdValue =
                            match userClientId with
                            | Email clientId -> clientId.Value
                            | PushSubscription x -> x.ToString()

                        let hash = clientIdValue |> computeMd5Hash "_SECRET_SALT_"
                        UserIdentity.Create hash)

                let ctx = Sql.GetDataContext(connString)
                async{
                    let! existing=
                        query{
                            for c in ctx.Main.LinkedIdentities do
                            where ((c.Identity =  userIdentity.Value.Value) && (c.ClientId = userClientId.ToString()))
                            select c
                        }
                        |> Seq.tryHeadAsync |> Async.AwaitTask
                    match existing with
                    | Some x ->return Ok (Version x.Version) 
                    | None ->
                        let linkedIdentity : LinkedIdentity = { Identity = userIdentity; ClientId = userClientId; Type = userClientId.Type; Version =Version 0L }
                        let row = 
                            ctx.Main.LinkedIdentities.``Create(CreatedAt, Document, Type, UpdatedAt, Version)``( System.DateTime.Now, encodeToBytes linkedIdentity, userClientId.Type, System.DateTime.Now, 0L)
                                
                        row.Identity <- userIdentity.Value.Value
                        row.ClientId <- userClientId.ToString()
                        
                        do! ctx.SubmitUpdatesAsync() |> Async.AwaitTask 
                        return Ok (Version 0L)
                        
                }
                
            let unlinkIdentity : UnlinkIdentity = 
                fun userIdentity userClientId ->
                let ctx = Sql.GetDataContext(connString)
                async{
                    let! existing=
                        query{
                            for c in ctx.Main.LinkedIdentities do
                            where ((c.Identity =  userIdentity.Value.Value) && (c.ClientId = userClientId.ToString()))
                            select c
                        }
                        |> Seq.tryHeadAsync |> Async.AwaitTask
                    match existing with
                    | Some x -> 
                        x.Delete()
                        do! ctx.SubmitUpdatesAsync() |> Async.AwaitTask 
                        return Ok (Version x.Version)
                    | None -> return Ok (Version 0L)
                }
            
            {| LinkedIdentity = linkIdentity; UnlinkedIdentity = unlinkIdentity |}