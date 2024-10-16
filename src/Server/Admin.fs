module AdminHandler

open Giraffe
open Scriban
open FSharp.Data.LiteralProviders
open System.IO
open Master
open FCQRS.ModelQuery
open AlarmsGlobal.Shared.Model.Subscription
open FCQRS.Model
open System.Security.Claims
open AlarmsGlobal.ServerInterfaces.Command

let template = TextFile.wwwroot.html.``admin.html``.Text

let publish env : HttpHandler =
    fun next ctx ->
        task {
            let isAuth = ctx.User.Identity.IsAuthenticated && ctx.User.IsInRole("admin")

            if not isAuth then
                return! setStatusCode 401 earlyReturn ctx
            else
                let cid = CID.CreateNew()
                let subs = env :> ISubscription
                let form = ctx.Request.Form
                let region = form.["region"][0] |> RegionId.Create
                let text = form.["message"][0] |> LongString.TryCreate |> forceValidate
                let globalEventId = GlobalEventId.CreateNew()

                let globalEvent: GlobalEvent = {
                    GlobalEventId = globalEventId
                    TargetRegion = region
                    Body = text
                    EventDateInUTC = System.DateTime.UtcNow |> Some
                }
                let! _ = subs.PublishEvent cid globalEvent
                return! setStatusCode 200 earlyReturn ctx
        }

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
