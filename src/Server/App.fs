module AppHandler

open Giraffe
open Scriban
open FSharp.Data.LiteralProviders
open System.IO
open Master

let appTemplate = TextFile.wwwroot.html.``app.html``.Text

let appHandler: HttpHandler =
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

                let! body = Template.Parse(template).RenderAsync().AsTask()
                return! renderInMaster body next ctx
        }
