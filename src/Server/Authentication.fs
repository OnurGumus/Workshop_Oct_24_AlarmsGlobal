module Authentication

open Giraffe
open Google.Apis.Auth
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open System.Security.Claims
open AlarmsGlobal.Environments
open Microsoft.Extensions.Configuration
open AlarmsGlobal.ServerInterfaces.Command
open AlarmsGlobal.Shared.Model
open AlarmsGlobal.ServerInterfaces.Query
open AlarmsGlobal.Shared.Model.Authentication
open System.Threading
open Microsoft.Extensions.Logging
open FCQRS.ModelQuery
open FCQRS.Model

let lockObj = obj ()

let prepareClaimsPrincipal (email: Email) (name: ShortString option) (env: _) =
    let config = env :> IConfiguration
    let auth = env :> IAuthentication
    let query = env :> IQuery<_>
    let loggerf = env :> ILoggerFactory
    let userClientId = Email email
    let userClientIdString = userClientId.ToString()

    async {
        let linkedQuery =
            query.Query<LinkedIdentity>(Equal("ClientId", userClientIdString), take = 1)

        let! users = linkedQuery

        let! user =
            match users with
            | [] ->
                lock lockObj (fun () ->
                    async {
                        // query again
                        let! users = linkedQuery

                        match users with
                        | [] ->
                            let cid = CID.CreateNew()

                            let subs =
                                query.Subscribe((fun e -> e.CID = cid), 1, ignore, CancellationToken.None) |> Async.StartImmediateAsTask
                            let! _ = auth.LinkIdentity cid None userClientId
                            do! subs |> Async.AwaitTask
                            let! users = linkedQuery
                            return users |> List.head

                        | user :: _ -> return user
                    })
            | user :: _ -> async { return user }

        let admins =
            config.GetSection("config:admins").AsEnumerable()
            |> Seq.map (fun x -> x.Value)
            |> Seq.filter (isNull >> not)
            |> Set.ofSeq

        let clientEmail =
            match user.ClientId with
            | Email x -> x
            | PushSubscription _ -> failwith "PushSubscription not supported"

        let name =
            match name with
            | Some x -> x.Value
            | None -> email.Value

        let claims = [
            Claim(ClaimTypes.Name, user.Identity.Value.Value)
            Claim(ClaimTypes.Email, email.Value)
            Claim(ClaimTypes.NameIdentifier, clientEmail.Value)
            Claim(ClaimTypes.GivenName, name)
        ]

        let claims =
            if admins |> Set.contains email.Value then
                Claim(ClaimTypes.Role, "admin") :: claims
            else
                claims

        return
            ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
            |> ClaimsPrincipal

    }

let signGoogleHandler (env: _) : HttpHandler =
    fun next ctx ->
        task {
            let credentials = ctx.Request.Form["credential"][0]
            let! payload = GoogleJsonWebSignature.ValidateAsync(credentials)

            let email = Email.TryCreate payload.Email |> forceValidate
            let name = ShortString.TryCreate payload.Name |> forceValidate |> Some
            let! principal = prepareClaimsPrincipal email name env

            let authProps = AuthenticationProperties()
            authProps.IsPersistent <- true
            do! ctx.SignInAsync(principal, authProps) |> Async.AwaitTask

            ctx.SetHttpHeader("Location", "/app")
            return! setStatusCode 303 earlyReturn ctx
        }

let signOutHandler: HttpHandler =
    fun next ctx ->
        task {
            ctx.Response.Headers.Add("Clear-Site-Data", "\"cookies\", \"storage\", \"cache\", \"executionContexts\"")

            do!
                ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme)
                |> Async.AwaitTask

            ctx.SetHttpHeader("Location", "/")
            return! setStatusCode 303 earlyReturn ctx
        }
