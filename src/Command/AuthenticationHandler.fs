module internal AuthenticationHandler

open System.Security.Cryptography
open System.Text
open AlarmsGlobal.Shared.Model.Authentication
open AlarmsGlobal.Shared.Command.Authentication
open FCQRS.Model
open FCQRS.Common

open AlarmsGlobal.Command

let computeMd5Hash salt (input: string) =
    using (MD5.Create()) (fun md5Hash ->
        let input = input + salt
        // Compute the hash of the input string
        let bytes = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input))

        // Convert the byte array to a hexadecimal string
        let builder = StringBuilder(32)
        bytes |> Array.iter (fun byte -> builder.Append(byte.ToString("x2")) |> ignore)
        builder.ToString())

let unlinkIdentity (createSubs) : UnlinkIdentity =
    fun userIdentity userClientId ->

        let actorId = "User_" + userIdentity.Value.Value

        async {
            let! subscribe =
                createSubs actorId (Domain.User.Command.UnlinkUserClientId(userClientId)) (function
                    | Domain.User.UserClientIdUnlinked _ -> true
                    | Domain.User.UserClientIdAlreadyUnlinked _ -> true
                    | _ -> false)

            match subscribe with
            | {
                  EventDetails = Domain.User.UserClientIdUnlinked _
                  Version = v
              } -> return Ok(Version v)
            | {
                  EventDetails = Domain.User.UserClientIdAlreadyUnlinked _
                  Version = v
              } -> return Ok(Version v)
            | _ -> return failwithf "unexpected event %A" subscribe
        }

let linkIdentity computeMd5Hash (createSubs) : LinkIdentity =
    fun identityOption userClientId ->
        let userIdentity =
            identityOption
            |> Option.defaultWith (fun () ->
                let clientIdValue =
                    match userClientId with
                    | Email clientId -> clientId.Value
                    | PushSubscription x -> x.ToString()

                let hash = clientIdValue |> computeMd5Hash
                UserIdentity.Create hash)

        let actorId = "User_" + userIdentity.Value.Value

        async {
            let! subscribe =
                createSubs actorId (Domain.User.Command.LinkUserClientId(userClientId, userIdentity)) (function
                    | Domain.User.UserClientIdLinked _ -> true
                    | Domain.User.UserClientIdAlreadyLinked _ -> true
                    | _ -> false)

            match subscribe with
            | {
                  EventDetails = Domain.User.UserClientIdLinked _
                  Version = v
              } -> return Ok(Version v)
            | {
                  EventDetails = Domain.User.UserClientIdAlreadyLinked _
                  Version = v
              } -> return Ok(Version v)
            | _ -> return failwithf "unexpected event %A" subscribe
        }
