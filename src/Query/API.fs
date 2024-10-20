module AlarmsGlobal.Query.API

open Microsoft.Extensions.Configuration
open AlarmsGlobal.Shared.Model
open FSharp.Data.Sql.Common
open Projection
open AlarmsGlobal.Shared.Model.Authentication
open AlarmsGlobal.Shared.Command.Authentication
open FCQRS.Serialization
open FCQRS.Model
open SqlProvider


let queryApi (config: IConfiguration) actorApi =

    let connString = config.GetSection("config:connection-string").Value

    let query
        (
            ty: System.Type,
            filter,
            orderby,
            orderbydesc,
            thenby,
            thenbydesc,
            take: int option,
            skip,
            (cacheKey: string option)
        ) : Async<obj seq> =

        let ctx = Sql.GetDataContext(connString)

        let augment db =
            FCQRS.SQLProvider.Query.augmentQuery filter orderby orderbydesc thenby thenbydesc take skip db

        let res: seq<obj> =

            if ty = typeof<LinkedIdentity> then
                let q =
                    query {
                        for c in ctx.Main.LinkedIdentities do
                            select c
                    }

                augment <@ q @>
                |> Seq.map (fun x -> x.Document |> decodeFromBytes<LinkedIdentity> :> obj)


            else
                failwith "not implemented"



        async { return res }

    Projection.init connString actorApi query
