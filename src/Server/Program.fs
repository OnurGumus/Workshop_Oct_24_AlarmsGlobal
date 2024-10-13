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

bootstrapLogger ()

module Templates =
    let master = TextFile.wwwroot.html.``master.html``.Text
    let index = TextFile.wwwroot.html.``index.html``.Text
    let app = TextFile.wwwroot.html.``app.html``.Text

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
                    | Prod -> Templates.app

                let! body = Template.Parse(template).RenderAsync().AsTask()
                return! renderInMaster body next ctx
        }

let handlers env =
    choose [
        GET >=> route "/" >=> indexHandler
        GET >=> route "/app" >=> appHandler
        POST >=> route "/signout" >=> signOutHandler
        POST >=> route "/signin-google" >=> signGoogleHandler env
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
