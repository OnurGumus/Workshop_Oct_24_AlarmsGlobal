module AppHandler

open Giraffe
open Scriban
open FSharp.Data.LiteralProviders
open System.IO
open Master
open FCQRS.ModelQuery
open AlarmsGlobal.Shared.Model.Subscription

let appTemplate = TextFile.wwwroot.html.``app.html``.Text

let appHandler env : HttpHandler =
    fun next ctx ->
        task {
            let isAuth = ctx.User.Identity.IsAuthenticated

            if not isAuth then
                return! setStatusCode 401 earlyReturn ctx
            else
                let template =
                    match ctx with
                    | Development -> File.ReadAllText TextFile.wwwroot.html.``app.html``.Path
                    | Prod -> appTemplate

                let query = env :> IQuery<_>
                let! regions = query.Query<Region>()

                let regions =
                    regions |> List.map (fun r -> {| Id = r.RegionId.Value; Name = r.Name.Value |})

                let! body = Template.Parse(template).RenderAsync({| regions = regions |}).AsTask()
                return! renderInMaster body next ctx
        }
