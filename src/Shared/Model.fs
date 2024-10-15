module rec AlarmsGlobal.Shared.Model

open FCQRS.ModelValidation
open FCQRS.Model
open System

module Authentication =
    open System.Text.RegularExpressions

    type EmailError =
        | EmptyEmail
        | InvalidEmailAddress

    type Email =
        private
        | Email of string

        member this.Value = let (Email email) = this in email

        static member TryCreate(email: string) =
            let regex =
                //regex not containing '_Saga_'
                Regex(@"^(?!.*(_dot_|_Saga_|~)).*$", RegexOptions.IgnoreCase)

            let email = email.Trim().Replace(" ", "")

            single (fun t ->
                t.TestOne email
                |> t.MinLen 1 EmptyEmail
                |> t.MaxLen 50 InvalidEmailAddress
                |> t.Match regex InvalidEmailAddress
                |> t.Map(fun x ->
                    let lowerCase = x.ToLowerInvariant()

                    let email =
                        if lowerCase.Contains("@gmail") && lowerCase.Contains(".") then
                            let left = lowerCase.Split("@").[0]
                            let right = lowerCase.Split("@").[1]
                            let removeDots = left.Replace(".", "")
                            removeDots + "@" + right
                        else
                            lowerCase

                    Email email)
                |> t.End)

        static member Validate(s: Email) =
            s.Value |> Email.TryCreate |> forceValidate

    type UserIdentity =
        | UserIdentity of ShortString

        member this.Value = let (UserIdentity uid) = this in uid

        static member CreateNew() =
            "UserIdentity_" + Guid.NewGuid().ToString()
            |> ShortString.TryCreate
            |> forceValidate
            |> UserIdentity

        static member Create(s: string) =
            s |> ShortString.TryCreate |> forceValidate |> UserIdentity

        static member Validate(s: LongString) =
            s.Value |> ShortString.TryCreate |> forceValidate


    type SubscriptionKeys = { p256dh: string; auth: string }

    // Define the type representing the subscription
    type PushSubscription = {
        endpoint: string
        expirationTime: int option
        keys: SubscriptionKeys
    }


    type UserClientId =
        | Email of Email
        | PushSubscription of PushSubscription

        override this.ToString() =
            match this with
            | Email email -> email.Value
            | PushSubscription pushSubscription -> pushSubscription.endpoint

        member this.Type =
            match this with
            | Email _ -> "Email"
            | PushSubscription _ -> "PushSubscription"

    type LinkedIdentity = {
        ClientId: UserClientId
        Identity: UserIdentity
        Version: FCQRS.Model.Version
        Type: string
    }

    type UserSettings = {
        Identity: UserIdentity
        Version: Version
        Settings: string
    }

    type User = {
        Identity: UserIdentity
        Email: Email
        Version: Version
        LinkedIdentities: LinkedIdentity list
        Settings: UserSettings option
    }

    type VerificationError =
        | EmptyVerificationCode
        | InvalidVerificationCode

    type VerificationCode =
        private
        | VerificationCode of string

        member this.Value = let (VerificationCode s) = this in s

        static member TryCreate(s: string) =
            single (fun t ->
                t.TestOne s
                |> t.MinLen 1 EmptyVerificationCode
                |> t.MaxLen 6 InvalidVerificationCode
                |> t.Map VerificationCode
                |> t.End)

    type LoginError = string
    type LogoutError = string

    type Subject = ShortString
    type Body = LongString

module Subscription =
    type RegionType =
        | Country
        | World

    type Tag = ShortString

    type RegionId =
        | RegionId of ShortString

        member this.Value = let (RegionId rid) = this in rid

        static member CreateNew() =
            "Region_" + Guid.NewGuid().ToString()
            |> ShortString.TryCreate
            |> forceValidate
            |> RegionId

        static member Create(s: string) =
            s |> ShortString.TryCreate |> forceValidate |> RegionId

        static member Validate(s: LongString) =
            s.Value |> ShortString.TryCreate |> forceValidate

    type Region = {
        RegionId: RegionId
        RegionType: RegionType
        ParentRegionId: RegionId option
        AlrernateNames: ShortString list
        Name: ShortString
    }

    
    type UserSubscription = {
        Identity: Authentication.UserIdentity
        RegionId: RegionId
    }


    type GlobalEventId =
        | GlobalEventId of ShortString

        member this.Value = let (GlobalEventId rid) = this in rid

        static member CreateNew() =
            "GlobalEvent_" + Guid.NewGuid().ToString()
            |> ShortString.TryCreate
            |> forceValidate
            |> GlobalEventId

        static member Create(s: string) =
            s |> ShortString.TryCreate |> forceValidate |> GlobalEventId

        static member Validate(s: LongString) =
            s.Value |> ShortString.TryCreate |> forceValidate

    
    type GlobalEvent = {
        GlobalEventId: GlobalEventId
        Title: ShortString
        Body: LongString
        Tags: Tag list
        EventDateInUTC: DateTime option
        Source: ShortString option
        Impact: int option
        TargetRegion: RegionId list
    } with

        override this.ToString() = this.Title.Value
