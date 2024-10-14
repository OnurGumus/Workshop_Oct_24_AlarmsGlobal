module AlarmsGlobal.Environments

open Microsoft.Extensions.Configuration
open AlarmsGlobal.ServerInterfaces.Command
open AlarmsGlobal.ServerInterfaces.Query
open System
open Microsoft.Extensions.Logging
open Shared.Command.Authentication
open FCQRS.ModelQuery
open FCQRS.Model

type AppEnv(config: IConfiguration, loggerFactory: ILoggerFactory) as self=

    let mutable queryApi = Unchecked.defaultof<_>
    let mutable commandApi = Unchecked.defaultof<_>

    interface ITimeProvider with
        member _.TimeProvider = TimeProvider.System


    interface ILoggerFactory with
        member this.AddProvider(provider: ILoggerProvider) : unit = loggerFactory.AddProvider(provider)

        member this.CreateLogger(categoryName: string) : ILogger =
            loggerFactory.CreateLogger(categoryName)

        member this.Dispose() : unit = loggerFactory.Dispose()

    interface IConfiguration with
        member _.Item
            with get (key: string) = config.[key]
            and set key v = config.[key] <- v

        member _.GetChildren() = config.GetChildren()
        member _.GetReloadToken() = config.GetReloadToken()
        member _.GetSection key = config.GetSection(key)

    interface IAuthentication with
        member this.LinkIdentity(cid: CID) : LinkIdentity = commandApi.LinkIdentity cid
        member this.UnlinkIdentity(cid: CID) : UnlinkIdentity = commandApi.UnlinkIdentity cid

    interface IQuery with
        member _.Query<'t>(?filter, ?orderby, ?orderbydesc, ?thenby, ?thenbydesc, ?take, ?skip, ?cacheKey) =
            async {
                let! res = queryApi (typeof<'t>, filter, orderby, orderbydesc, thenby, thenbydesc, take, skip, cacheKey)
                return res |> Seq.cast<'t> |> List.ofSeq
            }

        member _.Subscribe(cb, cancellationToken) = ()
        member _.Subscribe(filter, take, cb, cancellationToken) = 
                async { 
                    return () 
            }

    member this.Reset() = 
        Migrations.reset config
        this.Init()


    member _.Init() = 
        Migrations.init config
        queryApi <- AlarmsGlobal.Query.API.queryApi config
        commandApi <- AlarmsGlobal.Command.API.api self

