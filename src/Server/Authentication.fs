module Authentication

open Giraffe
open Google.Apis.Auth
open Microsoft.AspNetCore.Authentication
open Microsoft.AspNetCore.Authentication.Cookies
open System.Security.Claims

let signGoogleHandler: HttpHandler =
    fun next ctx ->
        task {
            let credentials = ctx.Request.Form["credential"][0]
            let! payload = GoogleJsonWebSignature.ValidateAsync(credentials)

            let authProps = AuthenticationProperties()
            authProps.IsPersistent <- true

            let claim = Claim(ClaimTypes.Name, payload.Email)
            let identity = new ClaimsIdentity([|claim|], CookieAuthenticationDefaults.AuthenticationScheme)
            let p = new ClaimsPrincipal(identity)
            do! ctx.SignInAsync(p, authProps) |> Async.AwaitTask

            return! text "Sign in with Google" next ctx
        }
