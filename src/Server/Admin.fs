module AdminHandler

open Giraffe
open Scriban
open FSharp.Data.LiteralProviders
open System.IO
open FCQRS.ModelQuery
open AlarmsGlobal.Shared.Model.Subscription
open FCQRS.Model
open System.Security.Claims
open AlarmsGlobal.ServerInterfaces.Command

let handler env : HttpHandler =
    fun next ctx ->
        task {
            let isAuth = ctx.User.Identity.IsAuthenticated && ctx.User.IsInRole("admin")

            if not isAuth then
                return! setStatusCode 401 earlyReturn ctx
            else
                let template =
                    match ctx with
                    | Development -> File.ReadAllText TextFile.wwwroot.html.``admin.html``.Path
                    | Prod -> template

                let query = env :> IQuery<_>
                let! regions = query.Query<Region>()

                let regions =
                    regions |> List.map (fun r -> {| Id = r.RegionId.Value; Name = r.Name.Value |})

                let! body = Template.Parse(template).RenderAsync({| regions = regions |}).AsTask()
                return! renderInMaster body next ctx
        }
