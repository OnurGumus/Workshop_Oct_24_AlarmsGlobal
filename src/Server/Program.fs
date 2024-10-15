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
open Master
open AppHandler
open FCQRS.Model
open FCQRS.ModelQuery
open System.Threading
open Microsoft.Extensions.Logging
open AlarmsGlobal.ServerInterfaces.Command
open AlarmsGlobal.Shared.Model.Authentication
open System.Security.Claims
open AlarmsGlobal.Shared.Model.Subscription

bootstrapLogger ()

module Templates =
    let master = TextFile.wwwroot.html.``master.html``.Text
    let index = TextFile.wwwroot.html.``index.html``.Text



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

let subscribe env : HttpHandler =
    fun next ctx ->
        task {
            let cid = CID.CreateNew()
            let query = env :> IQuery<_>
            let s =
                query.Subscribe((fun e -> e.CID = cid), 1, ignore, CancellationToken.None) |> Async.StartImmediateAsTask
            let subs = env :> ISubscription
            let form = ctx.Request.Form
            let region = form.["region"][0] |> RegionId.Create
            let identity = ctx.User.FindFirst(ClaimTypes.Name).Value |> UserIdentity.Create
            let! _ = subs.Subscribe cid (Some identity) region
            do! s |> Async.AwaitTask
            return! appHandler env next ctx
        }

let handlers env =
    choose [
        GET >=> route "/" >=> indexHandler
        GET >=> route "/app" >=> appHandler env
        POST >=> route "/signout" >=> signOutHandler
        POST >=> route "/signin-google" >=> signGoogleHandler env
        POST >=> route "/subscribe" >=> subscribe env
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
        appEnv.Init()

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
