module Master

open Microsoft.Extensions.Hosting
open Giraffe
open Scriban
open FSharp.Data.LiteralProviders
open System.IO
open Microsoft.AspNetCore.Http

let masterTemplate = TextFile.wwwroot.html.``master.html``.Text

let (|Development|Prod|) (ctx: HttpContext) =
    if ctx.GetWebHostEnvironment().EnvironmentName = Environments.Development then
        Development
    else
        Prod

let renderInMaster (body: string) : HttpHandler =
    fun next ctx ->
        task {
            let masterTemplate =
                match ctx with
                | Development -> File.ReadAllText TextFile.wwwroot.html.``master.html``.Path
                | Prod -> masterTemplate

            let template = Template.Parse(masterTemplate)
            let! page = template.RenderAsync({| body = body |})
            return! htmlString page next ctx
        }
