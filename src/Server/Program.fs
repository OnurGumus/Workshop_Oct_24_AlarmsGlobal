open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Builder
open Giraffe
open Scriban
open FSharp.Data.LiteralProviders
open System.IO
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open System
open Authentication
open Serilog
open AlarmsGlobal.Environments
open Hocon.Extensions.Configuration
open Microsoft.AspNetCore.Http
open FCQRS.ModelQuery
open AlarmsGlobal.Shared.Model.Subscription
open System.Security.Claims
open FCQRS.Model
open System.Threading
open AlarmsGlobal.ServerInterfaces.Command
open AlarmsGlobal.Shared.Model.Authentication

bootstrapLogger ()

module Templates =
    let master = TextFile.wwwroot.html.``master.html``.Text
    let index = TextFile.wwwroot.html.``index.html``.Text
    let app = TextFile.wwwroot.html.``app.html``.Text
    let admin = TextFile.wwwroot.html.``admin.html``.Text

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
                | Prod -> Templates.master

            let template = Template.Parse(masterTemplate)
            let! page = template.RenderAsync({| body = body |})
            return! htmlString page next ctx
        }

let indexPage (ctx: HttpContext) (dataloginurl: string) =
    let template =
        match ctx with
        | Development -> File.ReadAllText TextFile.wwwroot.html.``index.html``.Path
        | Prod -> Templates.index

    Template.Parse(template).RenderAsync({| dataloginurl = dataloginurl |}).AsTask()

let indexHandler: HttpHandler =
    fun next ctx ->
        task {
            let dataloginurl = "https://localhost:10201/signin-google"
            let! body = indexPage ctx (dataloginurl)
            return! renderInMaster body next ctx
        }

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
                    | Prod -> Templates.app

                let query = env :> IQuery<_>
                let! regions = query.Query<Region>()
                let userName = ctx.User.FindFirst(ClaimTypes.Name).Value
                // UserIdentity
                let! subscriptions = query.Query<UserSubscription>(Predicate.Equal("UserIdentity", userName))

                let regions =
                    regions |> List.map (fun r -> {| Id = r.RegionId.Value; Name = r.Name.Value |})


                let! body =
                    Template
                        .Parse(template)
                        .RenderAsync({| regions = regions; subscriptions = subscriptions |})
                        .AsTask()

                return! renderInMaster body next ctx
        }

let subscribe env : HttpHandler =
    fun next ctx ->
        task {
            if not ctx.User.Identity.IsAuthenticated then
                return! setStatusCode 401 earlyReturn ctx
            else
                let cid = CID.CreateNew()
                let query = env :> IQuery<_>

                let dataEventSubs =
                    query.Subscribe((fun e -> e.CID = cid), 1, ignore, CancellationToken.None)
                    |> Async.StartImmediateAsTask

                let commandInterface = env :> ISubscription
                let form = ctx.Request.Form
                let region = form.["region"][0] |> RegionId.Create
                let identity = ctx.User.FindFirst(ClaimTypes.Name).Value |> UserIdentity.Create
                let userSubsc = { RegionId = region; Identity = identity }
                let! _ = commandInterface.Subscribe cid userSubsc
                do! dataEventSubs
                return! appHandler env next ctx
        }


let adminHandler env : HttpHandler =
    fun next ctx ->
        task {
            let isAuth = ctx.User.Identity.IsAuthenticated && ctx.User.IsInRole("admin")

            if not isAuth then
                return! setStatusCode 401 earlyReturn ctx
            else
                let template =
                    match ctx with
                    | Development -> File.ReadAllText TextFile.wwwroot.html.``admin.html``.Path
                    | Prod -> Templates.admin

                let query = env :> IQuery<_>
                let! regions = query.Query<Region>()

                let regions =
                    regions |> List.map (fun r -> {| Id = r.RegionId.Value; Name = r.Name.Value |})

                let! body = Template.Parse(template).RenderAsync({| regions = regions |}).AsTask()
                return! renderInMaster body next ctx
        }

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
let handlers env =
    choose [
        GET >=> route "/" >=> indexHandler
        GET >=> route "/app" >=> appHandler env
        GET >=> route "/admin" >=> adminHandler env
        POST >=> route "/signout" >=> signOutHandler
        POST >=> route "/signin-google" >=> signGoogleHandler env
        POST >=> route "/subscribe" >=> subscribe env
        POST >=> route "/publish" >=> publish env
    ]

let authenticationOptions (opt: AuthenticationOptions) =
    let auth = CookieAuthenticationDefaults.AuthenticationScheme
    opt.DefaultScheme <- auth
    opt.DefaultChallengeScheme <- auth
    opt.DefaultAuthenticateScheme <- auth

type Startup(config: IConfiguration) =
    member __.ConfigureServices(services: IServiceCollection) =
        services.AddAuthorization() |> ignore
        services.AddAuthentication(authenticationOptions).AddCookie() |> ignore
        services.AddSingleton<AppEnv>() |> ignore

        services.Configure<CookieAuthenticationOptions>(
            CookieAuthenticationDefaults.AuthenticationScheme,
            fun (options: CookieAuthenticationOptions) ->
                options.Cookie.Name <- "AlarmsGlobal.Auth"
                options.ExpireTimeSpan <- TimeSpan.FromDays(7)
                options.SlidingExpiration <- true
        )
        |> ignore

    member __.Configure(app: IApplicationBuilder, env: IWebHostEnvironment, appEnv: AppEnv) =
        appEnv.Reset()

        app
            .UseAuthentication()
            .UseAuthorization()
            .UseSerilogRequestLogging()
            .UseStaticFiles()
            .UseGiraffe(handlers appEnv)


[<EntryPoint>]
let main argv =
    Host
        .CreateDefaultBuilder()
        .ConfigureAppConfiguration(fun _ configBuilder ->
            configBuilder
                .AddHoconFile("config.hocon", true)
                .AddHoconFile("secrets.hocon", false)
                .AddEnvironmentVariables()
            |> ignore)
        .ConfigureWebHostDefaults(fun webBuilder -> webBuilder.UseStartup<Startup>() |> ignore)
        .UseSerilog(configureLogging)
        .Build()
        .Run()

    0
