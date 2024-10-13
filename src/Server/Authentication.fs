module Authentication

open Giraffe
open Google.Apis.Auth

let signGoogleHandler: HttpHandler =
    fun next ctx ->
        task {
            let credentials = ctx.Request.Form["credential"][0]
            let! payload = GoogleJsonWebSignature.ValidateAsync(credentials)
            printfn "User ID: %s" payload.Email

            return! text "Sign in with Google" next ctx
        }
