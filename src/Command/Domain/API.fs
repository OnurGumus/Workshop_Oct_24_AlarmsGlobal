module internal Command.Domain.API

open FCQRS
open Akkling
open Akka
open Common
open Actor
open Akkling.Cluster.Sharding
open AlarmsGlobal.Command.Domain

let sagaCheck (env: _) toEvent actorApi (o: obj) =
    match o with
    | _ -> []

[<Interface>]
type IDomain =
    abstract ActorApi: IActor
    abstract UserFactory: string -> IEntityRef<obj>
    

let api (env: #_) (actorApi: IActor) =
        let toEvent ci =
            Common.toEvent actorApi.System.Scheduler ci
        let scr = (sagaCheck env toEvent actorApi)
        
        SagaStarter.init actorApi.System actorApi.Mediator scr
        User.Actor.init env toEvent actorApi |> ignore

        System.Threading.Thread.Sleep(1000)

        { new IDomain with
            member _.ActorApi = actorApi

            member _.UserFactory entityId =
                User.Actor.factory env toEvent actorApi entityId
        }
    