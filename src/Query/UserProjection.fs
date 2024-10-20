module UserProjection

open FSharp.Data.Sql.Common

open AlarmsGlobal.Shared.Model.Authentication
open FCQRS.Serialization
open FCQRS.Model
open Akka.Persistence.Query
open FCQRS.ModelQuery
open AlarmsGlobal.ServerInterfaces.Query
open AlarmsGlobal.Command.Domain
open SqlProvider

let handle (ctx: Sql.dataContext) eventDetails cid =
    let cid = CID.Create cid

    match eventDetails with

    | User.UserClientIdUnlinked(clientId, userIdentity) ->
        let encded = clientId.ToString()

        let findRow =
            query {
                for c in (ctx.Main.LinkedIdentities) do
                    where (c.Identity = userIdentity.Value.Value && c.ClientId = encded)

                    take 1
                    select c
            }
            |> Seq.tryHead
        //delete old row
        match findRow with
        | Some r -> r.Delete()
        | None -> ()

        let dataEvent = {
            Type = AutenticationEvent(AccountLinked(userIdentity, clientId))
            CID = cid
        }

        Some dataEvent
    | User.UserClientIdLinked(userClientId, userIdentity) ->
        let linkedIdentity: LinkedIdentity = {
            Identity = userIdentity
            ClientId = userClientId
            Type = userClientId.Type
            Version = Version 0L
        }

        let row =
            ctx.Main.LinkedIdentities.``Create(CreatedAt, Document, Type, UpdatedAt, Version)`` (
                System.DateTime.UtcNow,
                encodeToBytes linkedIdentity,
                userClientId.Type,
                System.DateTime.UtcNow,
                0L
            )

        row.Identity <- userIdentity.Value.Value
        row.ClientId <- userClientId.ToString()

        Some {
            Type = AutenticationEvent(AccountLinked(userIdentity, userClientId))
            CID = cid
        }

    | User.UserClientIdAlreadyLinked(clientId, identity) ->
        Some {
            Type = AutenticationEvent(AccountLinked(identity, clientId))
            CID = cid
        }
    | User.UserClientIdAlreadyUnlinked(clientId, identity) ->
        Some {
            Type = AutenticationEvent(AccountLinked(identity, clientId))
            CID = cid
        }
